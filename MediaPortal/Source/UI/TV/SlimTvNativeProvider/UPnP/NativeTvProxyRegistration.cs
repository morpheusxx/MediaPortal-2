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
using MediaPortal.Common;
using MediaPortal.Common.Messaging;
using MediaPortal.Common.Runtime;
using MediaPortal.Common.Services.ServerCommunication;
using MediaPortal.Common.UPnP;
using MediaPortal.Plugins.SlimTv.Interfaces;
using MediaPortal.Plugins.SlimTv.Interfaces.UPnP.DataTypes;
using MediaPortal.Plugins.SlimTv.UPnP;
using MediaPortal.UI.ServerCommunication;
using UPnP.Infrastructure.CP;
using UPnP.Infrastructure.CP.DeviceTree;

namespace MediaPortal.Plugins.SlimTv.Providers.UPnP
{
  public class NativeTvProxyRegistration
  {
    protected AsynchronousMessageQueue _messageQueue;

    static NativeTvProxyRegistration()
    {
      UPnPExtendedDataTypes.AddDataType(UPnPDtChannelGroupList.Instance);
      UPnPExtendedDataTypes.AddDataType(UPnPDtChannelList.Instance);
      UPnPExtendedDataTypes.AddDataType(UPnPDtProgram.Instance);
      UPnPExtendedDataTypes.AddDataType(UPnPDtProgramList.Instance);
      UPnPExtendedDataTypes.AddDataType(UPnPDtSchedule.Instance);
      UPnPExtendedDataTypes.AddDataType(UPnPDtScheduleList.Instance);
      UPnPExtendedDataTypes.AddDataType(UPnPDtLiveTvMediaItem.Instance);
    }

    public NativeTvProxyRegistration ()
    {
      RegisterService();
    }

    public void RegisterService()
    {
      UPnPClientControlPoint controlPoint = ServiceRegistration.Get<IServerConnectionManager>().ControlPoint;
      if (controlPoint == null)
        return;

      controlPoint.RegisterAdditionalService(RegisterNativeTvProxy);
      
      SubscribeToMessages();
    }

    public void UnregisterService()
    {
      _messageQueue.Shutdown();
      ServiceRegistration.RemoveAndDispose<ITvProvider>();
    }

    void SubscribeToMessages()
    {
      _messageQueue = new AsynchronousMessageQueue(this, new string[] { SystemMessaging.CHANNEL });
      _messageQueue.PreviewMessage += OnMessageReceived;
      _messageQueue.Start();
    }

    void OnMessageReceived(AsynchronousMessageQueue queue, SystemMessage message)
    {
      if (message.ChannelName == SystemMessaging.CHANNEL)
      {
        SystemMessaging.MessageType messageType = (SystemMessaging.MessageType) message.MessageType;
        if (messageType == SystemMessaging.MessageType.SystemStateChanged)
        {
          SystemState state = (SystemState) message.MessageData[SystemMessaging.NEW_STATE];
          switch (state)
          {
            case SystemState.ShuttingDown:
              UnregisterService();
              break;
          }
        }
      }
    }


    public NativeTvProxy RegisterNativeTvProxy(DeviceConnection connection)
    {
      CpService tvStub = connection.Device.FindServiceByServiceId(Consts.SLIMTV_SERVICE_ID);

      if (tvStub == null)
        throw new NotSupportedException("NativeTvService not supported by this UPnP device.");

      NativeTvProxy tvProxy = new NativeTvProxy(tvStub);
      return tvProxy;
    }
  }
}
