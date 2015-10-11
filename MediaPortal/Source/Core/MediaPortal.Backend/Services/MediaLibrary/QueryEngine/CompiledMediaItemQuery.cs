#region Copyright (C) 2007-2015 Team MediaPortal

/*
    Copyright (C) 2007-2015 Team MediaPortal
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
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using MediaPortal.Backend.Services.Database;
using MediaPortal.Common;
using MediaPortal.Common.MediaManagement;
using MediaPortal.Common.MediaManagement.MLQueries;
using MediaPortal.Backend.Database;
using MediaPortal.Utilities;
using MediaPortal.Utilities.Exceptions;
using MediaPortal.Common.Logging;

namespace MediaPortal.Backend.Services.MediaLibrary.QueryEngine
{
  /// <summary>
  /// Contains compiled media item query data, creates an SQL query for the query attributes, executes the query and picks out
  /// the result.
  /// </summary>
  public class CompiledMediaItemQuery
  {
    protected readonly MIA_Management _miaManagement;
    protected readonly ICollection<MediaItemAspectMetadata> _necessaryRequestedMIAs;
    protected readonly ICollection<MediaItemAspectMetadata> _optionalRequestedMIAs;
    protected readonly IDictionary<MediaItemAspectMetadata.AttributeSpecification, QueryAttribute> _mainSelectAttributes;
    protected readonly ICollection<MediaItemAspectMetadata.AttributeSpecification> _explicitSelectAttributes;
    protected readonly IFilter _filter;
    protected uint? _offset;
    protected uint? _limit;

    protected readonly IList<SortInformation> _sortInformation;

    public CompiledMediaItemQuery(
        MIA_Management miaManagement,
        ICollection<MediaItemAspectMetadata> necessaryRequestedMIAs,
        ICollection<MediaItemAspectMetadata> optionalRequestedMIAs,
        IDictionary<MediaItemAspectMetadata.AttributeSpecification, QueryAttribute> mainSelectedAttributes,
        ICollection<MediaItemAspectMetadata.AttributeSpecification> explicitSelectedAttributes,
        IFilter filter, IList<SortInformation> sortInformation,
        uint? limit = null,
        uint? offset = null)
    {
      _miaManagement = miaManagement;
      _necessaryRequestedMIAs = necessaryRequestedMIAs;
      _optionalRequestedMIAs = optionalRequestedMIAs;
      _mainSelectAttributes = mainSelectedAttributes;
      _explicitSelectAttributes = explicitSelectedAttributes;
      _filter = filter;
      _sortInformation = sortInformation;
      _limit = limit;
      _offset = offset;
    }

    public IDictionary<MediaItemAspectMetadata.AttributeSpecification, QueryAttribute> MainSelectAttributes
    {
      get { return _mainSelectAttributes; }
    }

    public ICollection<MediaItemAspectMetadata.AttributeSpecification> ExplicitSelectAttributes
    {
      get { return _explicitSelectAttributes; }
    }

    public IFilter Filter
    {
      get { return _filter; }
    }

    public ICollection<SortInformation> SortInformation
    {
      get { return _sortInformation; }
    }

    /// <summary>
    /// Optional offset to return items from a specific starting position from query.
    /// </summary>
    public uint? Offset
    {
      get { return _offset; }
    }

    /// <summary>
    /// Optional limit to return only a specific number of items from query.
    /// </summary>
    public uint? Limit
    {
      get { return _limit; }
    }

    public static CompiledMediaItemQuery Compile(MIA_Management miaManagement, MediaItemQuery query)
    {
      IDictionary<Guid, MediaItemAspectMetadata> availableMIATypes = miaManagement.ManagedMediaItemAspectTypes;
      ICollection<MediaItemAspectMetadata> necessaryMIATypes = new List<MediaItemAspectMetadata>();
      ICollection<MediaItemAspectMetadata> optionalMIATypes = new List<MediaItemAspectMetadata>();
      // Raise exception if necessary MIA types are not present
      foreach (Guid miaTypeID in query.NecessaryRequestedMIATypeIDs)
      {
        MediaItemAspectMetadata miam;
        if (!availableMIATypes.TryGetValue(miaTypeID, out miam))
          throw new InvalidDataException("Necessary requested MIA type '{0}' is not present in the media library", miaTypeID);
        necessaryMIATypes.Add(miam);
      }
      // For optional MIA types, we don't raise an exception if the type is not present
      foreach (Guid miaTypeID in query.OptionalRequestedMIATypeIDs)
      {
        MediaItemAspectMetadata miam;
        if (!availableMIATypes.TryGetValue(miaTypeID, out miam))
          continue;
        optionalMIATypes.Add(miam);
      }

      // Maps (all selected main) MIAM.Attributes to QueryAttributes
      IDictionary<MediaItemAspectMetadata.AttributeSpecification, QueryAttribute> mainSelectedAttributes = new Dictionary<MediaItemAspectMetadata.AttributeSpecification, QueryAttribute>();

      // Attributes selected in explicit queries
      ICollection<MediaItemAspectMetadata.AttributeSpecification> explicitSelectAttributes = new List<MediaItemAspectMetadata.AttributeSpecification>();

      // Allocate selected attributes to main query and explicit selects
      ICollection<Guid> requestedMIATypeIDs = CollectionUtils.UnionSet(query.NecessaryRequestedMIATypeIDs, query.OptionalRequestedMIATypeIDs);
      foreach (Guid miaTypeID in requestedMIATypeIDs)
      {
        MediaItemAspectMetadata miam;
        if (!availableMIATypes.TryGetValue(miaTypeID, out miam))
          // If one of the necessary MIA types is not available, an exception was raised above. So we only
          // come to here if an optional MIA type is not present - simply ignore that.
          continue;
        foreach (MediaItemAspectMetadata.AttributeSpecification attr in miam.AttributeSpecifications.Values)
        {
          if (attr.Cardinality == Cardinality.Inline || attr.Cardinality == Cardinality.ManyToOne)
            mainSelectedAttributes[attr] = new QueryAttribute(attr);
          else
            explicitSelectAttributes.Add(attr);
        }
      }

      return new CompiledMediaItemQuery(miaManagement, necessaryMIATypes, optionalMIATypes, mainSelectedAttributes, explicitSelectAttributes,
        query.Filter, query.SortInformation, query.Limit, query.Offset);
    }

    private IList<MediaItem> GetMediaItems(ISQLDatabase database, ITransaction transaction, bool singleMode, IEnumerable<MediaItemAspectMetadata> selectedMIAs, out IList<Guid> mediaItemIds)
    {
          IList<MediaItem> mediaItems = new List<MediaItem>();
          mediaItemIds = new List<Guid>();

        string statementStr;
        IList<BindVar> bindVars;

        // 1. Request all complex attributes
        IDictionary<Guid, IDictionary<MediaItemAspectMetadata.AttributeSpecification, ICollection<object>>> complexAttributeValues =
            new Dictionary<Guid, IDictionary<MediaItemAspectMetadata.AttributeSpecification, ICollection<object>>>();
        foreach (MediaItemAspectMetadata.AttributeSpecification attr in _explicitSelectAttributes)
        {
          ComplexAttributeQueryBuilder complexAttributeQueryBuilder = new ComplexAttributeQueryBuilder(
              _miaManagement, attr, null, _necessaryRequestedMIAs, _filter);
          using (IDbCommand command = transaction.CreateCommand())
          {
            string mediaItemIdAlias;
            string valueAlias;
            complexAttributeQueryBuilder.GenerateSqlStatement(out mediaItemIdAlias, out valueAlias,
                out statementStr, out bindVars);
            command.CommandText = statementStr;
            foreach (BindVar bindVar in bindVars)
              database.AddParameter(command, bindVar.Name, bindVar.Value, bindVar.VariableType);

            Type valueType = attr.AttributeType;
            using (IDataReader reader = command.ExecuteReader())
            {
              while (reader.Read())
              {
                Guid mediaItemId = database.ReadDBValue<Guid>(reader, reader.GetOrdinal(mediaItemIdAlias));
                object value = database.ReadDBValue(valueType, reader, reader.GetOrdinal(valueAlias));
                IDictionary<MediaItemAspectMetadata.AttributeSpecification, ICollection<object>> attributeValues;
                if (!complexAttributeValues.TryGetValue(mediaItemId, out attributeValues))
                  attributeValues = complexAttributeValues[mediaItemId] =
                      new Dictionary<MediaItemAspectMetadata.AttributeSpecification, ICollection<object>>();
                ICollection<object> attrValues;
                if (!attributeValues.TryGetValue(attr, out attrValues))
                  attrValues = attributeValues[attr] = new HashSet<object>();
                attrValues.Add(value);
              }
            }
          }
        }

        // 2. Main query
        MainQueryBuilder mainQueryBuilder = new MainQueryBuilder(_miaManagement,
            _mainSelectAttributes.Values, null, _necessaryRequestedMIAs, _optionalRequestedMIAs, _filter, _sortInformation, _limit, _offset);

        using (IDbCommand command = transaction.CreateCommand())
        {
          string mediaItemIdAlias2;
          IDictionary<MediaItemAspectMetadata, string> miamAliases;
          // Maps (selected and filtered) QueryAttributes to CompiledQueryAttributes in the SQL query
          IDictionary<QueryAttribute, string> qa2a;
          builder.GenerateSqlStatement(out mediaItemIdAlias2, out miamAliases, out qa2a,
              out statementStr, out bindVars);

          // Try to use SQL side paging, which gives best performance if supported
          ISQLDatabasePaging paging = database as ISQLDatabasePaging;
          if (paging != null)
            paging.Process(ref statementStr, ref bindVars, ref _offset, ref _limit);

          command.CommandText = statementStr;

          foreach (BindVar bindVar in bindVars)
            database.AddParameter(command, bindVar.Name, bindVar.Value, bindVar.VariableType);

          using (IDataReader fullReader = command.ExecuteReader())
          {

            var records = fullReader.AsEnumerable();
            if (_offset.HasValue)
              records = records.Skip((int)_offset.Value);
            if (_limit.HasValue)
              records = records.Take((int)_limit.Value);
            foreach (var reader in records)
            {
              Guid mediaItemId = database.ReadDBValue<Guid>(reader, reader.GetOrdinal(mediaItemIdAlias2));
              if (mediaItemIds.Contains(mediaItemId))
                // Media item was already added to result - query results are not always unique because of JOINs used for filtering
                continue;
              mediaItems.Add(mediaItemId);
              IDictionary<MediaItemAspectMetadata.AttributeSpecification, ICollection<object>> attributeValues;
              if (!complexAttributeValues.TryGetValue(mediaItemId, out attributeValues))
                attributeValues = null;
              MediaItem mediaItem = new MediaItem(mediaItemId);
              foreach (SingleMediaItemAspectMetadata miam in selectedMIAs.Where(x => x is SingleMediaItemAspectMetadata))
              {
                string name;
                if (!miamAliases.TryGetValue(miam, out name) || reader.IsDBNull(reader.GetOrdinal(name)))
                  // MIAM is not available for current media item
                  continue;
                SingleMediaItemAspect mia = new SingleMediaItemAspect(miam);
                foreach (MediaItemAspectMetadata.AttributeSpecification attr in miam.AttributeSpecifications.Values)
                  if (attr.Cardinality == Cardinality.Inline)
                  {
                    QueryAttribute qa = _mainSelectAttributes[attr];
                    string alias = qa2a[qa];
                    mia.SetAttribute(attr, database.ReadDBValue(attr.AttributeType, reader, reader.GetOrdinal(alias)));
                  }
                MediaItemAspect.SetAspect(mediaItem.Aspects, mia);
              }
              mediaItems.Add(mediaItem);
              if (singleMode)
              {
                  break;
              }
            }
          }
        }

        return mediaItems;
    }

    private void AddComplexAttributes(ISQLDatabase database, ITransaction transaction, IEnumerable<Guid> ids, IDictionary<Guid, IDictionary<MediaItemAspectMetadata.AttributeSpecification, ICollection<object>>> complexAttributeValues)
    {
        string statementStr;
        IList<BindVar> bindVars;

        foreach (MediaItemAspectMetadata.AttributeSpecification attr in _explicitSelectAttributes)
        {
            ComplexAttributeQueryBuilder builder = new ComplexAttributeQueryBuilder(
                _miaManagement, attr, null, _necessaryRequestedMIAs, new MediaItemIdFilter(ids));
            using (IDbCommand command = transaction.CreateCommand())
            {
                string mediaItemIdAlias;
                string valueAlias;
                builder.GenerateSqlStatement(out mediaItemIdAlias, out valueAlias,
                    out statementStr, out bindVars);
                command.CommandText = statementStr;
                foreach (BindVar bindVar in bindVars)
                    database.AddParameter(command, bindVar.Name, bindVar.Value, bindVar.VariableType);

                Type valueType = attr.AttributeType;
                using (IDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        Guid mediaItemId = database.ReadDBValue<Guid>(reader, reader.GetOrdinal(mediaItemIdAlias));
                        object value = database.ReadDBValue(valueType, reader, reader.GetOrdinal(valueAlias));
                        IDictionary<MediaItemAspectMetadata.AttributeSpecification, ICollection<object>> attributeValues;
                        if (!complexAttributeValues.TryGetValue(mediaItemId, out attributeValues))
                            attributeValues = complexAttributeValues[mediaItemId] =
                                new Dictionary<MediaItemAspectMetadata.AttributeSpecification, ICollection<object>>();
                        ICollection<object> attrValues;
                        if (!attributeValues.TryGetValue(attr, out attrValues))
                            attrValues = attributeValues[attr] = new List<object>();
                        attrValues.Add(value);
                    }
                }
            }
        }
    }

    private void AddMultipleMIAs(ISQLDatabase database, ITransaction transaction, IEnumerable<MediaItemAspectMetadata> selectedMIAs, IEnumerable<Guid> ids, IDictionary<Guid, ICollection<MultipleMediaItemAspect>> multipleMiaValues)
    {
        ILogger logger = ServiceRegistration.Get<ILogger>();

        foreach (MultipleMediaItemAspectMetadata miam in selectedMIAs.Where(x => x is MultipleMediaItemAspectMetadata))
        {
            MultipleMIAQueryBuilder builder = new MultipleMIAQueryBuilder(_miaManagement,
              _mainSelectAttributes.Values, null, miam, ids.ToArray(), _sortInformation);
            using (IDbCommand command = transaction.CreateCommand())
            {
                string mediaItemIdAlias;
                string indexAlias;
                // Maps (selected and filtered) QueryAttributes to CompiledQueryAttributes in the SQL query
                IDictionary<QueryAttribute, string> qa2a;
                string statementStr;
                IList<BindVar> bindVars;
                builder.GenerateSqlStatement(out mediaItemIdAlias, out indexAlias, out qa2a,
                  out statementStr, out bindVars);
                command.CommandText = statementStr;
                foreach (BindVar bindVar in bindVars)
                    database.AddParameter(command, bindVar.Name, bindVar.Value, bindVar.VariableType);

                using (IDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        Guid itemId = database.ReadDBValue<Guid>(reader, reader.GetOrdinal(mediaItemIdAlias));
                        ICollection<MultipleMediaItemAspect> values = null;
                        if (!multipleMiaValues.TryGetValue(itemId, out values))
                        {
                            values = new List<MultipleMediaItemAspect>();
                            multipleMiaValues[itemId] = values;
                        }
                        int index = database.ReadDBValue<Int32>(reader, reader.GetOrdinal(indexAlias));
                        MultipleMediaItemAspect mia = new MultipleMediaItemAspect(index, miam);
                        foreach (MediaItemAspectMetadata.AttributeSpecification attr in miam.AttributeSpecifications.Values)
                        {
                            if (attr.Cardinality == Cardinality.Inline)
                            {
                                QueryAttribute qa = _mainSelectAttributes[attr];
                                string alias = qa2a[qa];
                                logger.Debug("Reading multiple MIA attibute " + attr.AttributeName + " #" + index + " from column " + alias);
                                mia.SetAttribute(attr, database.ReadDBValue(attr.AttributeType, reader, reader.GetOrdinal(alias)));
                            }
                        }
                        values.Add(mia);
                    }
                }
            }
        }
    }

    public IList<MediaItem> QueryList()
    {
        return Query(false);
    }

    public IList<MediaItem> Query(bool singleMode)
    {
      ILogger logger = ServiceRegistration.Get<ILogger>();
      ISQLDatabase database = ServiceRegistration.Get<ISQLDatabase>();
      ITransaction transaction = database.BeginTransaction();

      try
      {
          IList<MediaItemAspectMetadata> selectedMIAs = new List<MediaItemAspectMetadata>(_necessaryRequestedMIAs.Union(_optionalRequestedMIAs));

          IList<Guid> mediaItemIds;
          IList<MediaItem> mediaItems = GetMediaItems(database, transaction, singleMode, selectedMIAs, out mediaItemIds);

          //logger.Debug("CompiledMediaItemQuery::Query got media items IDs [{0}]", string.Join(",", mediaItemIds));

          // Split the ID list into batches
          IList<IList<Guid>> batch = new List<IList<Guid>>();
          int start = 0;
          while(start < mediaItemIds.Count)
          {
            int count = 500;
            if (start + count >= mediaItemIds.Count)
            {
                count = mediaItemIds.Count - start;
            }
            batch.Add(new List<Guid>(mediaItemIds.Skip(start).Take(count)));
            start += count;
          }

          IDictionary<Guid, IDictionary<MediaItemAspectMetadata.AttributeSpecification, ICollection<object>>> complexAttributeValues =
              new Dictionary<Guid, IDictionary<MediaItemAspectMetadata.AttributeSpecification, ICollection<object>>>();
          foreach(IList<Guid> item in batch)
            AddComplexAttributes(database, transaction, item, complexAttributeValues);

          foreach (MediaItem mediaItem in mediaItems)
            {
                foreach (SingleMediaItemAspectMetadata miam in selectedMIAs.Where(x => x is SingleMediaItemAspectMetadata))
                {
                    // Skip complex attributes for this MIA if it's not already in the media item
                    if(!mediaItem.Aspects.ContainsKey(miam.AspectId))
                        continue;
                    IDictionary<MediaItemAspectMetadata.AttributeSpecification, ICollection<object>> attributeValues;
                    if (!complexAttributeValues.TryGetValue(mediaItem.MediaItemId, out attributeValues))
                        continue;
                    SingleMediaItemAspect mia = MediaItemAspect.GetOrCreateAspect(mediaItem.Aspects, miam);
                    foreach (MediaItemAspectMetadata.AttributeSpecification attr in miam.AttributeSpecifications.Values)
                        if (attr.Cardinality != Cardinality.Inline)
                        {
                            ICollection<object> attrValues;
                            if (attributeValues != null && attributeValues.TryGetValue(attr, out attrValues))
                                mia.SetCollectionAttribute(attr, attrValues);
                        }
                }
            }

          IDictionary<Guid, ICollection<MultipleMediaItemAspect>> multipleMiaValues =
            new Dictionary<Guid, ICollection<MultipleMediaItemAspect>>();
          foreach (IList<Guid> item in batch)
              AddMultipleMIAs(database, transaction, selectedMIAs, item, multipleMiaValues);

          if (multipleMiaValues.Count > 0)
          {
              logger.Debug("Got multiple MIAs for " + string.Join(",", multipleMiaValues.Keys));
              foreach (MediaItem mediaItem in mediaItems)
              {
                  ICollection<MultipleMediaItemAspect> values;
                  if (!multipleMiaValues.TryGetValue(mediaItem.MediaItemId, out values))
                      continue;
                  foreach (MultipleMediaItemAspect value in values)
                  {
                      logger.Debug("Adding MIA " + value);
                      MediaItemAspect.AddAspect(mediaItem.Aspects, value);
                  }
              }
          }

          return mediaItems;
      }
      catch(Exception e)
      {
        logger.Error("Unable to query", e);
        throw e;
      }
      finally
      {
        transaction.Dispose();
      }
    }

    public MediaItem QueryMediaItem()
    {
        IList<MediaItem> mediaItems = Query(true);
        if(mediaItems.Count == 1)
        {
            return mediaItems[0];
        }
        return null;
    }

    public override string ToString()
    {
      StringBuilder result = new StringBuilder();
      result.Append("CompiledMediaItemQuery\r\n");
      foreach (MediaItemAspectMetadata.AttributeSpecification attr in _explicitSelectAttributes)
      {
        ComplexAttributeQueryBuilder complexAttributeQueryBuilder = new ComplexAttributeQueryBuilder(
            _miaManagement, attr, null, _necessaryRequestedMIAs, _filter);
        result.Append("External attribute query for ");
        result.Append(attr.ParentMIAM.Name);
        result.Append(".");
        result.Append(attr.AttributeName);
        result.Append(":\r\n");
        result.Append(complexAttributeQueryBuilder);
        result.Append("\r\n\r\n");
      }
      result.Append("Main query:\r\n");
      SingleMIAQueryBuilder mainQueryBuilder = new SingleMIAQueryBuilder(_miaManagement,
          _mainSelectAttributes.Values, null, _necessaryRequestedMIAs, _optionalRequestedMIAs, _filter, _sortInformation, _limit, _offset);
      result.Append(mainQueryBuilder);
      return result.ToString();
    }
  }

  public static class DataReaderExtensions
  {
    public static IEnumerable<IDataReader> AsEnumerable(this IDataReader reader)
    {
      while (reader.Read())
        yield return reader;
    }
  }
}

