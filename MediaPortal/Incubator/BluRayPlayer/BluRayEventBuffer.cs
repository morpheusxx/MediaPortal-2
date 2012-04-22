using MediaPortal.Common;
using MediaPortal.Common.Logging;

namespace MediaPortal.UI.Players.Video
{
  public class BluRayEventBuffer
  {
    const int SIZE = 128;
    readonly BluRayAPI.BluRayEvent[] _buffer = new BluRayAPI.BluRayEvent[SIZE];
    int _readPos = 0;
    int _writePos = 0;

    public bool IsEmpty()
    {
      return _readPos == _writePos;
    }

    public int Count
    {
      get
      {
        int len = _writePos - _readPos;
        if (len < 0)
          len += SIZE;
        return len;
      }
    }

    public void Clear()
    {
      _writePos = 0;
      _readPos = 0;
    }

    public void Set(BluRayAPI.BluRayEvent data)
    {
      _buffer[_writePos] = data;
      _writePos = (_writePos + 1) % SIZE;
      if (_readPos == _writePos)
      {
        ServiceRegistration.Get<ILogger>().Warn("BluRayPlayer: Event buffer full");
      }
    }

    public BluRayAPI.BluRayEvent Peek()
    {
      return _buffer[_readPos];
    }

    public BluRayAPI.BluRayEvent Get()
    {
      int pos = _readPos;
      _readPos = (_readPos + 1) % SIZE;
      return _buffer[pos];
    }
  }
}
