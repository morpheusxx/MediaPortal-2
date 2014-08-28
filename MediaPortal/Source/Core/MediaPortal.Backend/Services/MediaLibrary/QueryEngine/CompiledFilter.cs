#region Copyright (C) 2007-2014 Team MediaPortal

/*
    Copyright (C) 2007-2014 Team MediaPortal
    http://www.team-mediaportal.com

    This file is part of MediaPortal 2

    MediaPortal 2 is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    MediaPortal 2 is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with MediaPortal 2. If not, see <http://www.gnu.org/licenses/>.
*/

#endregion

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MediaPortal.Common.MediaManagement;
using MediaPortal.Common.MediaManagement.MLQueries;
using MediaPortal.Utilities.Exceptions;
using MediaPortal.Utilities;
using MediaPortal.Common.MediaManagement.DefaultItemAspects;

namespace MediaPortal.Backend.Services.MediaLibrary.QueryEngine
{
  public class CompiledFilter
  {
    #region Constants

    public const int MAX_IN_VALUES_SIZE = 800;

    #endregion

    protected IList<object> _statementParts;
    protected IList<BindVar> _statementBindVars;

    protected readonly ICollection<QueryAttribute> _requiredAttributes;
    protected readonly ICollection<MediaItemAspectMetadata> _requiredMIATypes;
    protected readonly IDictionary<string, string> _innerJoinedTables = new Dictionary<string, string>();

    public CompiledFilter(MIA_Management miaManagement, IFilter filter, Namespace ns, BindVarNamespace bvNamespace, string outerMIIDJoinVariable, ICollection<TableJoin> tableJoins)
    {
      _statementParts = new List<object>();
      _statementBindVars = new List<BindVar>();
      _requiredMIATypes = new List<MediaItemAspectMetadata>();
      CompileStatementParts(miaManagement, filter, ns, bvNamespace, _requiredMIATypes, outerMIIDJoinVariable, tableJoins,
          _statementParts, _statementBindVars);
      _requiredAttributes = _statementParts.OfType<QueryAttribute>().ToList();
    }

    protected void CompileStatementParts(MIA_Management miaManagement, IFilter filter, Namespace ns, BindVarNamespace bvNamespace,
        ICollection<MediaItemAspectMetadata> requiredMIATypes, string outerMIIDJoinVariable, ICollection<TableJoin> tableJoins,
        IList<object> resultParts, IList<BindVar> resultBindVars)
    {
      if (filter == null)
        return;

      MediaItemIdFilter mediaItemIdFilter = filter as MediaItemIdFilter;
      if (mediaItemIdFilter != null)
      {
        ICollection<Guid> mediaItemIds = mediaItemIdFilter.MediaItemIds;
        if (mediaItemIds.Count == 0)
          resultParts.Add("1 = 2");
        else
        {
          if (mediaItemIds.Count == 1)
          {
            resultParts.Add(outerMIIDJoinVariable);
            BindVar bindVar = new BindVar(bvNamespace.CreateNewBindVarName("V"), mediaItemIds.First(), typeof(Guid));
            resultParts.Add(" = @" + bindVar.Name);
            resultBindVars.Add(bindVar);
          }
          else
          {
            bool first = true;
            ICollection<string> clusterExpressions = new List<string>();
            foreach (IList<Guid> mediaItemIdsCluster in CollectionUtils.Cluster(mediaItemIds, MAX_IN_VALUES_SIZE))
            {
              IList<string> bindVarRefs = new List<string>(MAX_IN_VALUES_SIZE);
              foreach (Guid mediaItemId in mediaItemIdsCluster)
              {
                BindVar bindVar = new BindVar(bvNamespace.CreateNewBindVarName("V"), mediaItemId, typeof(Guid));
                bindVarRefs.Add("@" + bindVar.Name);
                resultBindVars.Add(bindVar);
              }
              if (!first)
                resultParts.Add(" OR ");
              first = false;
              resultParts.Add(outerMIIDJoinVariable);
              resultParts.Add(" IN (" + StringUtils.Join(", ", bindVarRefs) + ")");
            }
            resultParts.Add(StringUtils.Join(" OR ", clusterExpressions));
          }
        }
        return;
      }

      BooleanCombinationFilter boolFilter = filter as BooleanCombinationFilter;
      if (boolFilter != null)
      {
        int numOperands = boolFilter.Operands.Count;
        IEnumerator enumOperands = boolFilter.Operands.GetEnumerator();
        if (!enumOperands.MoveNext())
          return;
        if (numOperands > 1)
          resultParts.Add("(");
        CompileStatementParts(miaManagement, (IFilter) enumOperands.Current, ns, bvNamespace,
            requiredMIATypes, outerMIIDJoinVariable, tableJoins, resultParts, resultBindVars);
        while (enumOperands.MoveNext())
        {
          switch (boolFilter.Operator)
          {
            case BooleanOperator.And:
              resultParts.Add(" AND ");
              break;
            case BooleanOperator.Or:
              resultParts.Add(" OR ");
              break;
            default:
              throw new NotImplementedException(string.Format(
                  "Boolean filter operator '{0}' isn't supported by the media library", boolFilter.Operator));
          }
          CompileStatementParts(miaManagement, (IFilter) enumOperands.Current, ns, bvNamespace,
              requiredMIATypes, outerMIIDJoinVariable, tableJoins, resultParts, resultBindVars);
        }
        if (numOperands > 1)
          resultParts.Add(")");
        return;
      }

      NotFilter notFilter = filter as NotFilter;
      if (notFilter != null)
      {
        resultParts.Add("NOT (");
        CompileStatementParts(miaManagement, notFilter.InnerFilter, ns, bvNamespace,
            requiredMIATypes, outerMIIDJoinVariable, tableJoins, resultParts, resultBindVars);
        resultParts.Add(")");
        return;
      }

      FalseFilter falseFilter = filter as FalseFilter;
      if (falseFilter != null)
      {
        resultParts.Add("1 = 2");
        return;
      }

      // Must be done before checking IAttributeFilter - EmptyFilter is also an IAttributeFilter but must be
      // compiled in a different way
      EmptyFilter emptyFilter = filter as EmptyFilter;
      if (emptyFilter != null)
      {
        MediaItemAspectMetadata.AttributeSpecification attributeType = emptyFilter.AttributeType;
        requiredMIATypes.Add(attributeType.ParentMIAM);
        Cardinality cardinality = attributeType.Cardinality;
        if (cardinality == Cardinality.Inline || cardinality == Cardinality.ManyToOne)
        {
          resultParts.Add(new QueryAttribute(attributeType));
          resultParts.Add(" IS NULL"); // MTO attributes are joined with left outer joins and thus can also be checked for NULL
        }
        else if (cardinality == Cardinality.OneToMany)
        {
          resultParts.Add("NOT EXISTS(");
          resultParts.Add("SELECT V.");
          resultParts.Add(MIA_Management.MIA_MEDIA_ITEM_ID_COL_NAME);
          resultParts.Add(" FROM ");
          resultParts.Add(miaManagement.GetMIACollectionAttributeTableName(attributeType));
          resultParts.Add(" V WHERE V.");
          resultParts.Add(MIA_Management.MIA_MEDIA_ITEM_ID_COL_NAME);
          resultParts.Add("=");
          resultParts.Add(outerMIIDJoinVariable);
          resultParts.Add(")");
        }
        else if (cardinality == Cardinality.ManyToMany)
        {
          resultParts.Add("NOT EXISTS(");
          resultParts.Add("SELECT NM.");
          resultParts.Add(MIA_Management.MIA_MEDIA_ITEM_ID_COL_NAME);
          resultParts.Add(" FROM ");
          resultParts.Add(miaManagement.GetMIACollectionAttributeNMTableName(attributeType));
          resultParts.Add(" NM INNER JOIN ");
          resultParts.Add(miaManagement.GetMIACollectionAttributeTableName(attributeType));
          resultParts.Add(" V ON NM.");
          resultParts.Add(MIA_Management.FOREIGN_COLL_ATTR_ID_COL_NAME);
          resultParts.Add(" = V.");
          resultParts.Add(MIA_Management.FOREIGN_COLL_ATTR_ID_COL_NAME);
          resultParts.Add(" WHERE NM.");
          resultParts.Add(MIA_Management.MIA_MEDIA_ITEM_ID_COL_NAME);
          resultParts.Add("=");
          resultParts.Add(outerMIIDJoinVariable);
          resultParts.Add(")");
        }
        return;
      }

      IAttributeFilter attributeFilter = filter as IAttributeFilter;
      if (attributeFilter != null)
      {
        // For attribute filters, we have to create different kinds of expressions, depending on the
        // cardinality of the attribute to be filtered.
        // For Inline and MTO attributes, we simply create
        //
        // QA [Operator] [Comparison-Value]
        //
        // for OTM attributes, we create
        //
        // INNER JOIN [OTM-Value-Table] V ON V.MEDIA_ITEM_ID=[Outer-Join-Variable-Placeholder]
        // WHERE [...] and V.VALUE [Operator] [Comparison-Value])
        //
        // for MTM attributes, we create
        //
        // INNER JOIN [MTM-NM-Table] NM ON NM.MEDIA_ITEM_ID=[Outer-Join-Variable-Placeholder]
        // INNER JOIN [MTM-Value-Table] V ON NM.ID = V.ID
        // WHERE [...] AND V.VALUE [Operator] [Comparison-Value])

        MediaItemAspectMetadata.AttributeSpecification attributeType = attributeFilter.AttributeType;
        requiredMIATypes.Add(attributeType.ParentMIAM);
        Cardinality cardinality = attributeType.Cardinality;
        if (cardinality == Cardinality.Inline || cardinality == Cardinality.ManyToOne)
          BuildAttributeFilterExpression(attributeFilter, new QueryAttribute(attributeType), bvNamespace,
              resultParts, resultBindVars);
        else if (cardinality == Cardinality.OneToMany)
        {
          string joinTable = miaManagement.GetMIACollectionAttributeTableName(attributeType);
          string attrName;
          if (!_innerJoinedTables.TryGetValue(joinTable, out attrName))
          {
            TableQueryData tqd = new TableQueryData(joinTable);

            tableJoins.Add(new TableJoin("LEFT OUTER JOIN", tqd,
                new RequestedAttribute(tqd, MIA_Management.MIA_MEDIA_ITEM_ID_COL_NAME), outerMIIDJoinVariable));
            attrName = new RequestedAttribute(tqd, MIA_Management.COLL_ATTR_VALUE_COL_NAME).GetQualifiedName(ns);
            _innerJoinedTables.Add(joinTable, attrName);
          }
          BuildAttributeFilterExpression(attributeFilter, attrName, bvNamespace, resultParts, resultBindVars);
        }
        else if (cardinality == Cardinality.ManyToMany)
        {
          string miaCollectionAttributeNMTableName = miaManagement.GetMIACollectionAttributeNMTableName(attributeType);
          string attrName;
          if (!_innerJoinedTables.TryGetValue(miaCollectionAttributeNMTableName, out attrName))
          {
            TableQueryData tqdMiaCollectionAttributeNMTable = new TableQueryData(miaCollectionAttributeNMTableName);

            tableJoins.Add(new TableJoin("LEFT OUTER JOIN", tqdMiaCollectionAttributeNMTable,
                new RequestedAttribute(tqdMiaCollectionAttributeNMTable, MIA_Management.MIA_MEDIA_ITEM_ID_COL_NAME), outerMIIDJoinVariable));

            TableQueryData tqdMiaCollectionAttributeTable = new TableQueryData(miaManagement.GetMIACollectionAttributeTableName(attributeType));

            tableJoins.Add(new TableJoin("LEFT OUTER JOIN", tqdMiaCollectionAttributeTable,
                new RequestedAttribute(tqdMiaCollectionAttributeNMTable, MIA_Management.FOREIGN_COLL_ATTR_ID_COL_NAME),
                new RequestedAttribute(tqdMiaCollectionAttributeTable, MIA_Management.FOREIGN_COLL_ATTR_ID_COL_NAME)));
            attrName = tqdMiaCollectionAttributeTable.GetAlias(ns) + "." + MIA_Management.COLL_ATTR_VALUE_COL_NAME;
            _innerJoinedTables.Add(miaCollectionAttributeNMTableName, attrName);
          }
          BuildAttributeFilterExpression(attributeFilter, attrName, bvNamespace, resultParts, resultBindVars);
        }
        return;
      }

      RelationshipFilter relationshipFilter = filter as RelationshipFilter;
      if(relationshipFilter != null)
      {
        resultParts.Add(outerMIIDJoinVariable);
        resultParts.Add(" IN (");
        CompileRelationshipCondition(miaManagement, bvNamespace, resultParts, resultBindVars,
          "R1", relationshipFilter, MediaLibrary_Relationships.ATTR_LEFT_ID, MediaLibrary_Relationships.ATTR_LEFT_TYPE, MediaLibrary_Relationships.ATTR_RIGHT_TYPE, MediaLibrary_Relationships.ATTR_RIGHT_ID);
        resultParts.Add(" UNION ");
        CompileRelationshipCondition(miaManagement, bvNamespace, resultParts, resultBindVars,
          "R2", relationshipFilter, MediaLibrary_Relationships.ATTR_RIGHT_ID, MediaLibrary_Relationships.ATTR_RIGHT_TYPE, MediaLibrary_Relationships.ATTR_LEFT_TYPE, MediaLibrary_Relationships.ATTR_LEFT_ID);
        resultParts.Add(")");
        return;
      }

      throw new InvalidDataException("Filter type '{0}' isn't supported by the media library", filter.GetType().Name);
    }

    private void CompileRelationshipCondition(MIA_Management miaManagement, BindVarNamespace bvNamespace,
      IList<object> resultParts, IList<BindVar> resultBindVars,
      string prefix, RelationshipFilter relationshipFilter,
      MediaItemAspectMetadata.AttributeSpecification attrId1, MediaItemAspectMetadata.AttributeSpecification attrType1,
      MediaItemAspectMetadata.AttributeSpecification attrType2, MediaItemAspectMetadata.AttributeSpecification attrId2
      )
    {
      resultParts.Add("SELECT ");
      resultParts.Add(prefix);
      resultParts.Add(".");
      resultParts.Add(miaManagement.GetMIAAttributeColumnName(attrId2));
      resultParts.Add(" FROM ");
      resultParts.Add(miaManagement.GetMIATableName(MediaLibrary_Relationships.Metadata));
      resultParts.Add(" ");
      resultParts.Add(prefix);
      resultParts.Add(" WHERE ");

      resultParts.Add(prefix);
      resultParts.Add(".");
      AddBindVar(miaManagement, bvNamespace, resultParts, resultBindVars, attrId1, prefix, relationshipFilter.ItemId.ToString(), typeof(string));

      resultParts.Add(" AND ");
      resultParts.Add(prefix);
      resultParts.Add(".");
      AddBindVar(miaManagement, bvNamespace, resultParts, resultBindVars, attrType1, prefix, relationshipFilter.ItemType.ToString(), typeof(string));
      
      resultParts.Add(" AND ");
      resultParts.Add(prefix);
      resultParts.Add(".");
      AddBindVar(miaManagement, bvNamespace, resultParts, resultBindVars, attrType2, prefix, relationshipFilter.RelationshipType.ToString(), typeof(string));
    }

    private void AddBindVar(MIA_Management miaManagement, BindVarNamespace bvNamespace,
      IList<object> resultParts, IList<BindVar> resultBindVars,
      MediaItemAspectMetadata.AttributeSpecification spec, string prefix, object value, Type valueType)
    {
      resultParts.Add(miaManagement.GetMIAAttributeColumnName(spec));
      resultParts.Add("=");
      BindVar bindVar = new BindVar(bvNamespace.CreateNewBindVarName(prefix), value, valueType);
      resultParts.Add("@" + bindVar.Name);
      resultBindVars.Add(bindVar);
    }

    /// <summary>
    /// Builds the actual filter SQL expression <c>[Attribute-Operand] [Operator] [Comparison-Value]</c> for the given
    /// attribute <paramref name="filter"/>.
    /// </summary>
    /// <param name="filter">Attribute filter instance to create the SQL expression for.</param>
    /// <param name="attributeOperand">Comparison attribute to be used. Depending on the cardinality of the
    /// to-be-filtered attribute, this will be the inline attribute alias or the attribute alias of the collection
    /// attribute table.</param>
    /// <param name="bvNamespace">Namespace used to build bind var names.</param>
    /// <param name="resultParts">Statement parts for the attribute filter.</param>
    /// <param name="resultBindVars">Bind variables for the attribute filter.</param>
    public static void BuildAttributeFilterExpression(IAttributeFilter filter, object attributeOperand, BindVarNamespace bvNamespace,
        IList<object> resultParts, IList<BindVar> resultBindVars)
    {
      Type attributeType = filter.AttributeType.AttributeType;
      RelationalFilter relationalFilter = filter as RelationalFilter;
      if (relationalFilter != null)
      {
        resultParts.Add(attributeOperand);
        switch (relationalFilter.Operator)
        {
          case RelationalOperator.EQ:
            resultParts.Add(" = ");
            break;
          case RelationalOperator.NEQ:
            resultParts.Add(" <> ");
            break;
          case RelationalOperator.LT:
            resultParts.Add(" < ");
            break;
          case RelationalOperator.LE:
            resultParts.Add(" <= ");
            break;
          case RelationalOperator.GT:
            resultParts.Add(" > ");
            break;
          case RelationalOperator.GE:
            resultParts.Add(" >= ");
            break;
          default:
            throw new NotImplementedException(string.Format(
                "Relational filter operator '{0}' isn't supported by the media library", relationalFilter.Operator));
        }
        BindVar bindVar = new BindVar(bvNamespace.CreateNewBindVarName("V"), relationalFilter.FilterValue, attributeType);
        resultParts.Add("@" + bindVar.Name);
        resultBindVars.Add(bindVar);
        return;
      }

      LikeFilter likeFilter = filter as LikeFilter;
      if (likeFilter != null)
      {
        if (!likeFilter.CaseSensitive)
          resultParts.Add("UPPER(");

        resultParts.Add(attributeOperand);

        if (!likeFilter.CaseSensitive)
          resultParts.Add(")");

        resultParts.Add(" LIKE ");

        BindVar bindVar = new BindVar(bvNamespace.CreateNewBindVarName("V"), likeFilter.Expression, attributeType);
        if (likeFilter.CaseSensitive)
          resultParts.Add("@" + bindVar.Name);
        else
          resultParts.Add("UPPER(@" + bindVar.Name + ")");
        resultBindVars.Add(bindVar);
        if (likeFilter.EscapeChar.HasValue)
        {
          bindVar = new BindVar(bvNamespace.CreateNewBindVarName("E"), likeFilter.EscapeChar.ToString(), typeof(Char));
          resultParts.Add(" ESCAPE @" + bindVar.Name);
          resultBindVars.Add(bindVar);
        }
        return;
      }

      BetweenFilter betweenFilter = filter as BetweenFilter;
      if (betweenFilter != null)
      {
        resultParts.Add(attributeOperand);
        resultParts.Add(" BETWEEN ");
        BindVar bindVar = new BindVar(bvNamespace.CreateNewBindVarName("V"), betweenFilter.Value1, attributeType);
        resultParts.Add("@" + bindVar.Name);
        resultBindVars.Add(bindVar);
        resultParts.Add(" AND ");
        bindVar = new BindVar(bvNamespace.CreateNewBindVarName("V"), betweenFilter.Value2, attributeType);
        resultParts.Add("@" + bindVar.Name);
        resultBindVars.Add(bindVar);
        return;
      }

      InFilter inFilter = filter as InFilter;
      if (inFilter != null)
      {
        if (inFilter.Values.Count == 0)
        {
          resultParts.Add("1 = 2"); // No comparison values means filter is always false
          return;
        }
        ICollection<string> clusterExpressions = new List<string>();
        foreach (IList<object> valuesCluster in CollectionUtils.Cluster(inFilter.Values, MAX_IN_VALUES_SIZE))
        {
          IList<string> bindVarRefs = new List<string>(MAX_IN_VALUES_SIZE);
          foreach (object value in valuesCluster)
          {
            BindVar bindVar = new BindVar(bvNamespace.CreateNewBindVarName("V"), value, attributeType);
            bindVarRefs.Add("@" + bindVar.Name);
            resultBindVars.Add(bindVar);
          }
          clusterExpressions.Add(" IN (" + StringUtils.Join(", ", bindVarRefs) + ")");
        }
        resultParts.Add(StringUtils.Join(" OR ", clusterExpressions));
        return;
      }
      throw new InvalidDataException("Filter type '{0}' isn't supported by the media library", filter.GetType().Name);
    }

    /// <summary>
    /// Returns a collection of <see cref="QueryAttribute"/> instances encapsulating attributes which are accesed by this filter
    /// and thus must be available in the SQL query.
    /// </summary>
    public ICollection<QueryAttribute> RequiredAttributes
    {
      get { return _requiredAttributes; }
    }

    /// <summary>
    /// Returns a collection of <see cref="MediaItemAspectMetadata"/> instances representing the MIA types that will be accessed by this
    /// filter.
    /// </summary>
    public ICollection<MediaItemAspectMetadata> RequiredMIATypes
    {
      get { return _requiredMIATypes; }
    }

    // outerMIIDJoinVariable is MEDIA_ITEMS.MEDIA_ITEM_ID (or its alias) for simple selects,
    // MIAM_TABLE_XXX.MEDIA_ITEM_ID (or alias) for complex selects, used for join conditions in complex filters
    public string CreateSqlFilterCondition(Namespace ns, IDictionary<QueryAttribute, RequestedAttribute> requestedAttributes,
        out IList<BindVar> bindVars)
    {
      StringBuilder filterBuilder = new StringBuilder(1000);
      foreach (object statementPart in _statementParts)
      {
        QueryAttribute qa = statementPart as QueryAttribute;
        filterBuilder.Append(qa == null ? statementPart.ToString() : requestedAttributes[qa].GetQualifiedName(ns));
      }
      bindVars = _statementBindVars;
      return filterBuilder.ToString();
    }

    /// <summary>
    /// Creates a simple SQL filter term from the given precompiled <paramref name="statementParts"/>.
    /// The given parts must have been compiled by method <see cref="BuildAttributeFilterExpression"/>.
    /// </summary>
    /// <param name="ns">Namespace in which the query is generated.</param>
    /// <param name="statementParts">Precompiled filter statement parts created by method <see cref="BuildAttributeFilterExpression"/>.</param>
    /// <param name="requestedAttributes">Dictionary containing all attributes which have been requested in the query which will accommodate
    /// the result filter. This dictionary must contain all attributes contained in <see cref="RequiredAttributes"/>.</param>
    /// <returns>Created filter string to be placed behind the <c>WHERE</c> keyword.</returns>
    public static string CreateSimpleSqlFilterCondition(Namespace ns, IList<object> statementParts,
        IDictionary<QueryAttribute, RequestedAttribute> requestedAttributes)
    {
      StringBuilder filterBuilder = new StringBuilder(1000);
      foreach (object statementPart in statementParts)
      {
        QueryAttribute qa = statementPart as QueryAttribute;
        filterBuilder.Append(qa == null ? statementPart.ToString() : requestedAttributes[qa].GetQualifiedName(ns));
      }
      return filterBuilder.ToString();
    }

    public override string ToString()
    {
      return StringUtils.Join(string.Empty, _statementParts);
    }
  }
}
