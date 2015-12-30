﻿using HttpServer;
using HttpServer.Sessions;
using MediaPortal.Common;
using MediaPortal.Common.Logging;
using MediaPortal.Plugins.MP2Extended.Attributes;
using MediaPortal.Plugins.MP2Extended.Exceptions;
using MediaPortal.Plugins.MP2Extended.ResourceAccess.TAS.EPG.BaseClasses;
using MediaPortal.Plugins.MP2Extended.TAS.Tv;
using MediaPortal.Plugins.SlimTv.Interfaces;
using MediaPortal.Plugins.SlimTv.Interfaces.Items;

namespace MediaPortal.Plugins.MP2Extended.ResourceAccess.TAS.EPG
{
  [ApiFunctionDescription(Type = ApiFunctionDescription.FunctionType.Json, Summary = "")]
  [ApiFunctionParam(Name = "channelId", Type = typeof(int), Nullable = false)]
  internal class GetNextProgramOnChannel : BaseProgramDetailed, IRequestMicroModuleHandler
  {
    public dynamic Process(IHttpRequest request, IHttpSession session)
    {
      HttpParam httpParam = request.Param;
      string channelId = httpParam["channelId"].Value;

      if (channelId == null)
        throw new BadRequestException("GetNextProgramOnChannel: channelId is null");

      int channelIdInt;
      if (!int.TryParse(channelId, out channelIdInt))
        throw new BadRequestException(string.Format("GetNextProgramOnChannel: Couldn't parse channelId to int: {0}", channelId));

      if (!ServiceRegistration.IsRegistered<ITvProvider>())
        throw new BadRequestException("GetNextProgramOnChannel: ITvProvider not found");

      IChannelAndGroupInfo channelAndGroupInfo = ServiceRegistration.Get<ITvProvider>() as IChannelAndGroupInfo;
      IProgramInfo programInfo = ServiceRegistration.Get<ITvProvider>() as IProgramInfo;


      IChannel channel;
      if (!channelAndGroupInfo.GetChannel(channelIdInt, out channel))
        throw new BadRequestException(string.Format("GetNextProgramOnChannel: Couldn't get channel with Id: {0}", channelIdInt));

      IProgram programNow;
      IProgram programNext;
      if (!programInfo.GetNowNextProgram(channel, out programNow, out programNext))
        Logger.Warn("GetNextProgramOnChannel: Couldn't get Now/Next Info for channel with Id: {0}", channelIdInt);

      WebProgramDetailed webProgramDetailed = ProgramDetailed(programNext);

      return webProgramDetailed;
    }

    internal static ILogger Logger
    {
      get { return ServiceRegistration.Get<ILogger>(); }
    }
  }
}