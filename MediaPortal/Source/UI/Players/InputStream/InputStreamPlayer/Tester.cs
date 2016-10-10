using System;
using System.Collections.Generic;
using MediaPortal.Common;
using MediaPortal.Common.MediaManagement;
using MediaPortal.Common.MediaManagement.DefaultItemAspects;
using MediaPortal.Common.Messaging;
using MediaPortal.Common.PluginManager;
using MediaPortal.Common.ResourceAccess;
using MediaPortal.Common.Runtime;
using MediaPortal.Common.Services.ResourceAccess.LocalFsResourceProvider;
using MediaPortal.Common.Services.ResourceAccess.RawUrlResourceProvider;
using MediaPortal.Common.SystemResolver;
using MediaPortal.UiComponents.Media.Models;
using MediaPortal.UI.Control.InputManager;
using MediaPortal.UI.Presentation.Actions;

namespace MediaPortal.UI.Players.InputStreamPlayer
{
  public class Tester : IPluginStateTracker
  {
    private AsynchronousMessageQueue _messageQueue;

    public void Activated(PluginRuntime pluginRuntime)
    {
      ISystemStateService sss = ServiceRegistration.Get<ISystemStateService>();
      if (sss.CurrentState == SystemState.Running)
      {
        IInputManager manager = ServiceRegistration.Get<IInputManager>(false);
        if (manager != null)
          manager.AddKeyBinding(Key.F8, new VoidKeyActionDlgt(TestPlayer));
      }
      else
      {
        _messageQueue = new AsynchronousMessageQueue(this, new string[]
          {
              SystemMessaging.CHANNEL
          });
        _messageQueue.MessageReceived += OnMessageReceived;
        _messageQueue.Start();
      }
    }

    void OnMessageReceived(AsynchronousMessageQueue queue, SystemMessage message)
    {
      if (message.ChannelName == SystemMessaging.CHANNEL)
      {
        SystemMessaging.MessageType messageType = (SystemMessaging.MessageType)message.MessageType;
        if (messageType == SystemMessaging.MessageType.SystemStateChanged)
        {
          SystemState newState = (SystemState)message.MessageData[SystemMessaging.NEW_STATE];
          if (newState == SystemState.Running)
          {
            IInputManager manager = ServiceRegistration.Get<IInputManager>(false);
            if (manager != null)
              manager.AddKeyBinding(Key.F8, new VoidKeyActionDlgt(TestPlayer));
          }
        }
      }
    }

    public bool RequestEnd()
    {
      return true;
    }

    public void Stop()
    {
      DropMessageQueue();
    }

    public void Continue()
    {
    }

    public void Shutdown()
    {
    }


    private void TestPlayer()
    {
      ISystemResolver systemResolver = ServiceRegistration.Get<ISystemResolver>();
      IDictionary<Guid, MediaItemAspect> aspects = new Dictionary<Guid, MediaItemAspect>();
      MediaItemAspect providerResourceAspect;
      MediaItemAspect mediaAspect;

      var resourceAccessor = new RawUrlResourceAccessor("http://s3.lvlt.dash.us.aiv-cdn.net/d/1$AOAGZA014O5RE,18A35628/videoquality$1080p/prod/65c3/a3e2/e5f7/4c03-9e12-3abc2687696b/430c2983-0b7d-40e1-ac55-cf05c6fc6f97_corrected.mpd");
      aspects[ProviderResourceAspect.ASPECT_ID] = providerResourceAspect = new MediaItemAspect(ProviderResourceAspect.Metadata);
      aspects[MediaAspect.ASPECT_ID] = mediaAspect = new MediaItemAspect(MediaAspect.Metadata);
      providerResourceAspect.SetAttribute(ProviderResourceAspect.ATTR_SYSTEM_ID, systemResolver.LocalSystemId);

      String raPath = resourceAccessor.CanonicalLocalResourcePath.Serialize();
      providerResourceAspect.SetAttribute(ProviderResourceAspect.ATTR_RESOURCE_ACCESSOR_PATH, raPath);

      // VideoAspect needs to be included to associate VideoPlayer later!
      aspects[VideoAspect.ASPECT_ID] = new MediaItemAspect(VideoAspect.Metadata);
      mediaAspect.SetAttribute(MediaAspect.ATTR_TITLE, "Stream Test");
      mediaAspect.SetAttribute(MediaAspect.ATTR_MIME_TYPE, "xxx/inputstream");
      MediaItem mediaItem = new MediaItem(Guid.Empty, aspects);

      PlayItemsModel.PlayItem(mediaItem);
    }
    protected void DropMessageQueue()
    {
      if (_messageQueue != null)
        _messageQueue.Terminate();
      _messageQueue = null;
    }
  }
}
