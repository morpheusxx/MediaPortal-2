// Copyright (C) 2005-2012 Team MediaPortal
// http://www.team-mediaportal.com
// 
// MediaPortal is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 2 of the License, or
// (at your option) any later version.
// 
// MediaPortal is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with MediaPortal. If not, see <http://www.gnu.org/licenses/>.

#include "madpresenter.h"

LPDIRECT3DDEVICE9 m_pDevice = NULL;
MPMadPresenter* m_pMadPresenter = NULL;
IBaseFilter* m_pMadFilter = NULL;

const DWORD D3DFVF_VID_FRAME_VERTEX = D3DFVF_XYZRHW | D3DFVF_TEX1;

struct VID_FRAME_VERTEX
{
  float x;
  float y;
  float z;
  float rhw;
  float u;
  float v;
};

// Init madVR Presenter (called by VideoPlayer.cs)
__declspec(dllexport) int MadVRInit(IMVRCallback* callback, int xposition, int yposition, int width, int height, DWORD dwD3DDevice, OAHWND parent, IBaseFilter** madFilter, IGraphBuilder* pMediaControl)
{
  Log("*********************************************************");
  Log("    Initializing MP2 madVR presenter version 0.0.0.1     ");
  Log("*********************************************************");

  m_pDevice = reinterpret_cast<LPDIRECT3DDEVICE9>(dwD3DDevice);
  m_pMadPresenter = new MPMadPresenter(callback, xposition, yposition, width, height, parent, m_pDevice, pMediaControl);
  m_pMadFilter = m_pMadPresenter->Initialize();

  *madFilter = m_pMadFilter;
  if (!madFilter)
    return S_FALSE;

  Log("MadVRInit: successfully created presenter");

  return S_OK;
}


__declspec(dllexport) void MadVRDeinit()
{
  try
  {
    if (m_pMadPresenter)
    {
      Log("MadVRDeinit: shutdown");
      m_pMadPresenter->DeInitialize();
    }
  }
  catch (...)
  {
  }
}

MPMadPresenter::MPMadPresenter(IMVRCallback* pCallback, int xposition, int yposition, int width, int height, OAHWND parent, IDirect3DDevice9* pDevice, IGraphBuilder* pGraphbuilder) :
  CUnknown(NAME("MPMadPresenter"), nullptr),
  m_pCallback(pCallback),
  m_dwGUIWidth(width),
  m_dwGUIHeight(height),
  m_hParent(parent),
  m_pDevice(static_cast<IDirect3DDevice9Ex*>(pDevice)),
  m_pGraphbuilder(pGraphbuilder)
{
  Log("MPMadPresenter::Constructor() instance 0x%x", this);
  m_Xposition = xposition;
  m_Yposition = yposition;
  m_pShutdown = false;
  m_pDevice->GetRenderTarget(0, &m_pSurfaceDevice);
}

/* Destructor */
MPMadPresenter::~MPMadPresenter()
{
  {
    //Log("MPMadPresenter::Destructor() graphbuilder release");
    //if (m_pGraphbuilder)
    //{
    //  m_pGraphbuilder = nullptr;
    //}

    if (m_pMad)
    {
      m_pMad.Release();
      m_pMad = nullptr;
    }

    //m_hWnd = nullptr;

    Log("MPMadPresenter::Destructor() instance 0x%x", this);
  }
}

void MPMadPresenter::SetMadVrPaused(bool paused)
{
  if (!m_pPausedDone && !m_pRunDone)
  {
    m_pPausedCount++;
    IMediaControl *m_pControl = nullptr;
    if ((m_pGraphbuilder) && (SUCCEEDED(m_pGraphbuilder->QueryInterface(__uuidof(IMediaControl), reinterpret_cast<LPVOID*>(&m_pControl)))) && (m_pControl))
    {
      if (m_pControl)
      {
        if (paused)
        {
          OAFilterState state;
          for (int i1 = 0; i1 < 10; i1++)
          {
            m_pControl->GetState(200, &state);
            if (state != State_Paused)
            {
              m_pControl->Pause();
              m_pPausedDone = true;
              Log("MPMadPresenter:::SetMadVrPaused() pause");
              Sleep(100);
            }
            else if (state == State_Paused && m_pPausedCount > 50)
            {
              m_pPausedDone = true;
            }
          }
        }
        if (!paused)
        {
          m_pControl->Run();
          m_pRunDone = true;
          Log("MPMadPresenter:::SetMadVrPaused() run");
        }
        m_pControl->Release();
        m_pControl = nullptr;
      }
    }
  }
}

void MPMadPresenter::RepeatFrame()
{
  if (m_pShutdown)
  {
    Log("MPMadPresenter::RepeatFrame() shutdown");
    return;
  }

  // Render frame to try to fix HD4XXX GPU flickering issue
  if (CComQIPtr<IMadVROsdServices> pOR = m_pMad)
  {
    pOR->OsdRedrawFrame();
  }
}

void MPMadPresenter::GrabScreenshot()
{
  if (m_pShutdown)
  {
    if (m_pCallback)
    {
      m_pCallback->GrabScreenshot(nullptr);
    }
    return;
  }

  try
  {
    if (m_pMad && m_pCallback)
    {
      if (CComQIPtr<IBasicVideo> m_pBV = m_pMad)
      {
        LONG nBufferSize = 0;
        HRESULT hr = E_NOTIMPL;
        hr = m_pBV->GetCurrentImage(&nBufferSize, NULL);
        if (hr != S_OK)
        {
          return;
        }
        long* ppData = static_cast<long *>(malloc(nBufferSize));
        hr = m_pBV->GetCurrentImage(&nBufferSize, ppData);
        if (hr != S_OK || !ppData)
        {
          free(ppData);
          return;
        }
        if (ppData)
        {
          PBITMAPINFO bi = PBITMAPINFO(ppData);
          PBITMAPINFOHEADER bih = &bi->bmiHeader;
          int bpp = bih->biBitCount;
          if (bpp != 16 && bpp != 24 && bpp != 32)
          {
            free(ppData);
            return;
          }
          m_pCallback->GrabScreenshot(LPVOID(ppData));
          free(ppData);
        }
      }
    }
  }
  catch (...)
  {
  }
}

void MPMadPresenter::GrabCurrentFrame()
{
  {
    try
    {
      if (m_pShutdown)
      {
        if (m_pCallback)
        {
          m_pCallback->GrabCurrentFrame(nullptr);
        }
        return;
      }
      if (m_pCallback && m_pMad)
      {
        if (CComQIPtr<IMadVRFrameGrabber> pMadVrFrame = m_pMad)
        {
          LPVOID dibImageBuffer = nullptr;
          pMadVrFrame->GrabFrame(ZOOM_50_PERCENT, FLAGS_NO_SUBTITLES | FLAGS_NO_ARTIFACT_REMOVAL | FLAGS_NO_IMAGE_ENHANCEMENTS | FLAGS_NO_UPSCALING_REFINEMENTS | FLAGS_NO_HDR_SDR_CONVERSION,
            CHROMA_UPSCALING_NGU_AA, IMAGE_DOWNSCALING_SSIM1D100, IMAGE_UPSCALING_NGU_SHARP_GRAIN, 0, &dibImageBuffer, nullptr);

          // Send the DIB to C#
          m_pCallback->GrabCurrentFrame(dibImageBuffer);
          LocalFree(dibImageBuffer);
          dibImageBuffer = nullptr;
          //Log("GrabFrame() hr");
        }
      }
    }
    catch (...)
    {
    }
  }
}

void MPMadPresenter::MadVr3DSizeRight(int x, int y, int width, int height)
{
  if (m_pMadD3DDev)
  {
    m_dwLeft = x;
    m_dwTop = y;
    m_dwWidth = width;
    m_dwHeight = height;
    Log("%s : init ok for Auto D3D : %d x %d", __FUNCTION__, width, height);
  }
}

void MPMadPresenter::MadVr3DSizeLeft(int x, int y, int width, int height)
{
  if (m_pMadD3DDev)
  {
    m_dwLeftLeft = x;
    m_dwTopLeft = y;
    m_dwWidthLeft = width;
    m_dwHeightLeft = height;
    Log("%s : init ok for Auto D3D : %d x %d", __FUNCTION__, width, height);
  }
}

void MPMadPresenter::MadVrScreenResize(int x, int y, int width, int height, bool displayChange)
{
  // Set window video position when screen change.
  Log("%s : SetWindowPos : %d x %d", __FUNCTION__, width, height);
  SetWindowPos(reinterpret_cast<HWND>(m_hParent), m_hWnd, x, y, width, height, SWP_ASYNCWINDOWPOS);

  // Needed to update OSD/GUI when changing directx present parameter on resolution change.
  if (displayChange)
  {
    if (m_pMadD3DDev)
    {
      // Needed to be set to true only if madVR device is ready
      m_pReInitOSD = true;
    }
    m_dwGUIWidth = width;
    m_dwGUIHeight = height;
    Log("%s : done : %d x %d", __FUNCTION__, width, height);
  }
}

void MPMadPresenter::MadVr3D(bool Enable)
{
  m_madVr3DEnable = Enable;
}

IBaseFilter* MPMadPresenter::Initialize()
{
  CAutoLock lock(this);

  CComQIPtr<IBaseFilter> baseFilter = nullptr;
  if (m_pMad)
  {
    baseFilter = m_pMad;
    return baseFilter;
  }

  HRESULT hr = m_pMad.CoCreateInstance(CLSID_madVR, GetOwner());
  if (FAILED(hr))
  {
    m_pMad = nullptr;
    return nullptr;
  }
  if (m_pMad)
  {
    if (baseFilter = m_pMad)
    {
      hr = m_pGraphbuilder->AddFilter(baseFilter, L"madVR");
      if (FAILED(hr))
      {
        m_pMad = nullptr;
        return nullptr;
      }
    }

    ULONG count = GetRefCount();

    m_pCallback->AddRef();
    Log("MPMadPresenter::Constructor() store device surface");
    // Store device surface MP GUI for later
    m_pCallback->RestoreDeviceSurface(m_pSurfaceDevice);

    // ISubRenderCallback
    CComQIPtr<ISubRender> pSR = m_pMad;
    if (!pSR)
    {
      m_pMad = nullptr;
      return nullptr;
    }

    m_pSRCB = new CSubRenderCallback(this);
    if (pSR && FAILED(pSR->SetCallback(m_pSRCB)))
    {
      m_pMad = nullptr;
      return nullptr;
    }
    m_pSRCB->AddRef();
    count = GetRefCount();

    // IOsdRenderCallback
    CComQIPtr<IMadVROsdServices> pOR = m_pMad;
    if (!pOR)
    {
      m_pMad = nullptr;
      return nullptr;
    }

    m_pORCB = new COsdRenderCallback(this);
    if (pOR && FAILED(pOR->OsdSetRenderCallback("MP2-GUI", m_pORCB)))
    {
      m_pMad = nullptr;
    }
    m_pORCB->AddRef();
    count = GetRefCount();

    m_hWnd = reinterpret_cast<HWND>(m_hParent);

    if (CComQIPtr<IVideoWindow> window = m_pMad)
    {
      window->put_Owner(m_hParent);
      window->put_MessageDrain(m_hParent);
      window->put_WindowStyle(WS_CHILD | WS_VISIBLE | WS_CLIPSIBLINGS | WS_CLIPCHILDREN);
      window->put_Left(m_Xposition);
      window->put_Top(m_Yposition);
      window->put_Height(m_dwGUIHeight);
      window->put_Width(m_dwGUIWidth);
    }
    /*CComQIPtr<IBasicVideo> video = CComQIPtr<IBasicVideo>(pRenderer);
    video->SetDestinationPosition(m_Xposition, m_Yposition, m_dwGUIWidth, m_dwGUIHeight);*/
    count = GetRefCount();

    // Configure initial Madvr Settings
    ConfigureMadvr();

    count = GetRefCount();

    return baseFilter;
  }
  m_pMad = nullptr;
  return nullptr;
}

void MPMadPresenter::DeInitialize()
{
  m_pShutdown = true;
  CAutoLock lock(this);

  if (!m_pMad)
  {
    return;
  }

  LONG count = GetRefCount();

  if (m_pMad)
  {
    if (CComQIPtr<IVideoWindow> window = m_pMad)
    {
      window->put_Visible(OAFALSE);
      window->put_MessageDrain(NULL);
      window->put_Owner(NULL);
    }
  }
  count = GetRefCount();

  //Sleep(2000);
  
  // Enable DisplayModeChanger is set by using DRR when player enters/leaves fullscreen (if we use profiles)
  EnableOriginalDisplayMode(true);

  if (m_pORCB)
  {
    // IOsdRenderCallback
    Log("MPMadPresenter::DeInitialize() osd render");
    CComQIPtr<IMadVROsdServices> pOR = m_pMad;
    if (!pOR)
    {
      m_pMad = nullptr;
      return;
    }

    if (pOR && FAILED(pOR->OsdSetRenderCallback("MP2-GUI", nullptr)))
    {
      m_pMad = nullptr;
      return;
    }

    // nasty, but we have to let it know about our death somehow
    static_cast<COsdRenderCallback*>(static_cast<IOsdRenderCallback*>(m_pORCB))->SetShutdownOsd(true);
    static_cast<COsdRenderCallback*>(static_cast<IOsdRenderCallback*>(m_pORCB))->SetDXRAP(nullptr);
    m_pORCB->Release();
    m_pORCB = nullptr;
  }
  count = GetRefCount();

  if (m_pSRCB)
  {
    // ISubRenderCallback
    Log("MPMadPresenter::DeInitialize() subtitle render");
    CComQIPtr<ISubRender> pSR = m_pMad;
    if (!pSR)
    {
      m_pMad = nullptr;
      return;
    }

    if (pSR && (FAILED(pSR->SetCallback(nullptr))))
    {
      m_pMad = nullptr;
      return;
    }

    // nasty, but we have to let it know about our death somehow
    static_cast<CSubRenderCallback*>(static_cast<ISubRenderCallback*>(m_pSRCB))->SetShutdownSub(true);
    static_cast<CSubRenderCallback*>(static_cast<ISubRenderCallback*>(m_pSRCB))->SetDXRAPSUB(nullptr);
    m_pSRCB->Release();
    m_pSRCB = nullptr;
  }
  count = GetRefCount();

  // Stop in current thread
  CComPtr<IMediaControl> m_pControl = nullptr;
  if ((m_pGraphbuilder) && (SUCCEEDED(m_pGraphbuilder->QueryInterface(__uuidof(IMediaControl), reinterpret_cast<LPVOID*>(&m_pControl)))) && (m_pControl))
  {
    if (m_pControl)
    {
      OAFilterState state;
      m_pControl->GetState(INFINITE, &state);
      if (state != State_Stopped)
      {
        m_pControl->Pause();
        m_pControl->GetState(1000, nullptr);

        m_pControl->Stop();
        m_pControl->GetState(1000, nullptr);

        for (int i1 = 0; i1 < 200; i1++)
        {
          m_pControl->GetState(INFINITE, &state);
          if (state == State_Stopped)
            break;
          Sleep(10);
        }
      }
      m_pControl = nullptr;
    }
  }
  count = GetRefCount();

  // Restore windowed overlay settings
  if (m_pMad)
  {
    if (CComQIPtr<IMadVRSettings> settings = m_pMad)
    {
      // Read enableOverlay settings
      settings->SettingsGetBoolean(L"enableOverlay", &m_enableOverlay);
      if (m_enableOverlay)
      {
        Log("MPMadPresenter::DeInitialize() disable windowed overlay mode");
        settings->SettingsSetBoolean(L"enableOverlay", false);

      }
    }
  }

  //Sleep(1000);

  if (m_pMadD3DDev != nullptr)
  {
    Log("MPMadPresenter::DeInitialize() release 3d device");
    m_pMadD3DDev.Release();
    m_pMadD3DDev = nullptr;
  }
  count = GetRefCount();

  if (m_pCallback)
  {
    Log("MPMadPresenter::DeInitialize() reset subtitle device");
    m_pCallback->SetSubtitleDevice(reinterpret_cast<LONG>(nullptr));
    Log("MPMadPresenter::DeInitialize() RestoreDeviceSurface");
    m_pCallback->RestoreDeviceSurface(m_pSurfaceDevice);

    Log("MPMadPresenter::DeInitialize() callback release");
    m_pCallback->Release();
    m_pCallback = nullptr;
  }
  count = GetRefCount();

  if (m_pDevice != nullptr)
  {
    m_pDevice.Release();
    m_pDevice = nullptr;
  }
  count = GetRefCount();

  if (m_pSurfaceDevice != nullptr)
  {
    m_pSurfaceDevice.Release();
    m_pSurfaceDevice = nullptr;
  }

  if (m_pGraphbuilder != nullptr)
  {
    m_pGraphbuilder.Release();
    m_pGraphbuilder = nullptr;
  }

  if (m_hParent)
  {
    m_hParent = NULL;
  }

  if (m_hWnd)
  {
    m_hWnd = NULL;
  }

  count = GetRefCount();

  Log("MPMadPresenter::DeInitialize() complete");
}

ULONG MPMadPresenter::GetRefCount()
{
  CComQIPtr<IBaseFilter> baseFilter = nullptr;
  if (baseFilter = m_pMad)
  {
    IBaseFilter* test = baseFilter;
    test->AddRef();
    return test->Release();
  }
  return 0;
}

STDMETHODIMP MPMadPresenter::SetGrabEvent(HANDLE pGrabEvent)
{
  m_pGrabEvent = pGrabEvent;
  return S_OK;
}

void MPMadPresenter::EnableExclusive(bool bEnable)
{
  if (m_pMad)
  {
    if (CComQIPtr<IMadVRCommand> pMadVrCmd = m_pMad)
    {
      pMadVrCmd->SendCommandBool("disableExclusiveMode", !bEnable);
    }
  }
};

void MPMadPresenter::EnableOriginalDisplayMode(bool bEnable)
{
  if (m_pMad)
  {
    if (CComQIPtr<IMadVRSettings> settings = m_pMad)
    {
      // Read DisplayModeChanger settings
      BOOL enableDisplayModeChanger;
      BOOL enableDisplayModeRestore;
      settings->SettingsGetBoolean(L"enableDisplayModeChanger", &enableDisplayModeChanger);
      settings->SettingsGetBoolean(L"restoreDisplayMode", &enableDisplayModeRestore);
      if (enableDisplayModeChanger)
      {
        settings->SettingsSetBoolean(L"enableDisplayModeChanger", true);
        settings->SettingsSetBoolean(L"changeDisplayModeOnPlay", false);
      }
      if (enableDisplayModeRestore)
      {
        settings->SettingsSetBoolean(L"restoreDisplayMode", true);
        settings->SettingsSetBoolean(L"restoreDisplayModeOnClose", false);
      }
    }
  }
};

void MPMadPresenter::ConfigureMadvr()
{
  if (m_pMad)
  {
    if (CComQIPtr<IMadVRCommand> cmd = m_pMad)
    {
      cmd->SendCommandBool("disableSeekbar", true);
      cmd->SendCommandString("setZoomMode", L"touchInside");
    }

    if (CComQIPtr<IMadVRDirect3D9Manager> manager = m_pMad)
    {
      manager->ConfigureDisplayModeChanger(true, true);
    }

    // TODO implement IMadVRSubclassReplacement (if enable, it 's breaking mouse event on FSE for MVC)
    /*if (CComQIPtr<IMadVRSubclassReplacement> pSubclassReplacement = m_pMad)
    {
      pSubclassReplacement->DisableSubclassing();
    }*/

    //if (Com::SmartQIPtr<IVideoWindow> pWindow = m_pMad) // Fix DXVA for FSE
    //{
    //  pWindow->SetWindowPosition(m_Xposition, m_Yposition, m_dwGUIWidth, m_dwGUIHeight);
    //  //pWindow->put_Owner(m_hParent);
    //}

    if (CComQIPtr<IMadVRSettings> settings = m_pMad)
    {
      // Read exclusive settings
      settings->SettingsGetBoolean(L"enableExclusive", &m_ExclusiveMode);
      if (m_ExclusiveMode)
      {
        //settings->SettingsSetBoolean(L"exclusiveDelay", true);
        settings->SettingsSetBoolean(L"enableExclusive", true);
      }
    }
  }
}

STDMETHODIMP MPMadPresenter::NonDelegatingQueryInterface(REFIID riid, void** ppv)
{
  if (riid != IID_IUnknown && m_pMad)
  {
    if (SUCCEEDED(m_pMad->QueryInterface(riid, ppv)))
    {
      return S_OK;
    }
  }

  return __super::NonDelegatingQueryInterface(riid, ppv);
}

HRESULT MPMadPresenter::ClearBackground(LPCSTR name, REFERENCE_TIME frameStart, RECT* fullOutputRect, RECT* activeVideoRect)
{
  return S_OK;

  HRESULT hr = E_UNEXPECTED;

  if (m_pShutdown)
  {
    Log("MPMadPresenter::ClearBackground() shutdown or init OSD");
    return hr;
  }

  SIZE szVideoFrame;
  if (CComQIPtr<IMadVRInfo> m_pMVRI = m_pMad)
  {
    if (m_pMVRI) 
    {
      // Use IMadVRInfo to get size. See http://bugs.madshi.net/view.php?id=180
      m_pMVRI->GetSize("originalVideoSize", &szVideoFrame);
    }
  }

  DWORD videoHeight = szVideoFrame.cy;
  DWORD videoWidth = szVideoFrame.cx;

  ReinitOSD(true);
  uiVisible = false;

  //Log("MPMadPresenter::ClearBackground()");

  if (!m_pMPTextureGui || !m_pMadGuiVertexBuffer || !m_pRenderTextureGui || !m_pCallback)
    return CALLBACK_INFO_DISPLAY;

  m_dwHeight = fullOutputRect->bottom - fullOutputRect->top; // added back
  m_dwWidth = fullOutputRect->right - fullOutputRect->left;

  RenderToTexture(m_pMPTextureGui);

  uiVisible = hr == S_OK ? true : false;

  //Log("ClearBackground() hr: 0x%08x - 2", hr);

  if (SUCCEEDED(hr = m_pDevice->PresentEx(nullptr, nullptr, nullptr, nullptr, D3DPRESENT_FORCEIMMEDIATE)))
    if (SUCCEEDED(hr = SetupMadDeviceState()))
      if (SUCCEEDED(hr = SetupOSDVertex(m_pMadGuiVertexBuffer)))
        // Draw MP texture on madVR device's side
        RenderTexture(m_pMadGuiVertexBuffer, m_pRenderTextureGui);

  // For 3D
  if (m_madVr3DEnable)
  {
    if (SUCCEEDED(hr = SetupOSDVertex3D(m_pMadGuiVertexBuffer)))
      // Draw MP texture on madVR device's side
      RenderTexture(m_pMadGuiVertexBuffer, m_pRenderTextureGui);
  }

  SetMadVrPaused(m_pPaused);

  return uiVisible ? CALLBACK_USER_INTERFACE : CALLBACK_INFO_DISPLAY;
}

HRESULT MPMadPresenter::RenderOsd(LPCSTR name, REFERENCE_TIME frameStart, RECT* fullOutputRect, RECT* activeVideoRect)
{
  return S_OK;

  HRESULT hr = E_UNEXPECTED;

  if (m_pShutdown)
  {
    Log("MPMadPresenter::RenderOsd() shutdown");
    return hr;
  }

  SIZE szVideoFrame;
  if (CComQIPtr<IMadVRInfo> m_pMVRI = m_pMad)
  {
    if (m_pMVRI) 
    {
      // Use IMadVRInfo to get size. See http://bugs.madshi.net/view.php?id=180
      m_pMVRI->GetSize("originalVideoSize", &szVideoFrame);
    }
  }

  DWORD videoHeight = szVideoFrame.cy;
  DWORD videoWidth = szVideoFrame.cx;

  //Log("%s : log activeVideoRect bottom x top : %d x %d", __FUNCTION__, (WORD)activeVideoRect->bottom, (WORD)activeVideoRect->top);
  //Log("%s : log activeVideoRect right x left : %d x %d", __FUNCTION__, (WORD)activeVideoRect->right, (WORD)activeVideoRect->left);
  //Log("%s : log for : %d x %d", __FUNCTION__, m_dwHeight, m_dwWidth);

  ReinitOSD(false);

  uiVisible = false;

  //Log("MPMadPresenter::RenderOsd()");

  if (!m_pMPTextureOsd || !m_pMadOsdVertexBuffer || !m_pRenderTextureOsd || !m_pCallback)
    return CALLBACK_INFO_DISPLAY;

  IDirect3DSurface9* SurfaceMadVr = nullptr; // This will be released by C# side

  m_dwHeight = (WORD)fullOutputRect->bottom - (WORD)fullOutputRect->top;
  m_dwWidth = (WORD)fullOutputRect->right - (WORD)fullOutputRect->left;

  //Log("%s : log fullOutputRect bottom x top : %d x %d", __FUNCTION__, (WORD)fullOutputRect->bottom, (WORD)fullOutputRect->top);
  //Log("%s : log fullOutputRect right x left : %d x %d", __FUNCTION__, (WORD)fullOutputRect->right, (WORD)fullOutputRect->left);
  //Log("%s : log for : %d x %d", __FUNCTION__, m_dwHeight, m_dwWidth);

  // For ambilight system but only working for D3D9
  if (SUCCEEDED(hr = m_pMadD3DDev->GetBackBuffer(0, 0, D3DBACKBUFFER_TYPE_MONO, &SurfaceMadVr)))
  {
    if (SUCCEEDED(hr = m_pCallback->RenderFrame(m_dwWidth, m_dwHeight, m_dwWidth, m_dwHeight, reinterpret_cast<LONG>(SurfaceMadVr))))
    {
      SurfaceMadVr->Release();
    }
  }

  RenderToTexture(m_pMPTextureOsd);

  uiVisible = hr == S_OK ? true : false;

  //Log("RenderOsd() hr: 0x%08x - 2", hr);

  if (SUCCEEDED(hr = m_pDevice->PresentEx(nullptr, nullptr, nullptr, nullptr, D3DPRESENT_FORCEIMMEDIATE)))
    if (SUCCEEDED(hr = SetupMadDeviceState()))
      if (SUCCEEDED(hr = SetupOSDVertex(m_pMadOsdVertexBuffer)))
        // Draw MP texture on madVR device's side
        RenderTexture(m_pMadOsdVertexBuffer, m_pRenderTextureOsd);

  // For 3D
  if (m_madVr3DEnable)
  {
    if (SUCCEEDED(hr = SetupOSDVertex3D(m_pMadOsdVertexBuffer)))
      // Draw MP texture on madVR device's side
      RenderTexture(m_pMadOsdVertexBuffer, m_pRenderTextureOsd);
  }

  SetEvent(m_pGrabEvent);

  SetMadVrPaused(m_pPaused);

  return uiVisible ? CALLBACK_USER_INTERFACE : CALLBACK_INFO_DISPLAY;
}

void MPMadPresenter::RenderToTexture(IDirect3DTexture9* pTexture)
{
  if (!m_pDevice)
    return;

  HRESULT hr = E_UNEXPECTED;
  IDirect3DSurface9* pSurface = nullptr; // This will be released by C# side
  if (SUCCEEDED(hr = pTexture->GetSurfaceLevel(0, &pSurface)))
  {
    if (SUCCEEDED(hr = m_pCallback->SetRenderTarget(reinterpret_cast<LONG>(pSurface))))
    {
      // TODO is it needed ?
      hr = m_pDevice->Clear(0, nullptr, D3DCLEAR_TARGET, D3DCOLOR(0), 1.0f, 0);
    }
  }
  //Log("RenderToTexture hr: 0x%08x", hr);
}

void MPMadPresenter::RenderTexture(IDirect3DVertexBuffer9* pVertexBuf, IDirect3DTexture9* pTexture)
{
  if (!m_pMadD3DDev)
    return;

  HRESULT hr = E_UNEXPECTED;
  if (SUCCEEDED(hr = m_pMadD3DDev->SetStreamSource(0, pVertexBuf, 0, sizeof(VID_FRAME_VERTEX))))
  {
    if (SUCCEEDED(hr = m_pMadD3DDev->SetTexture(0, pTexture)))
    {
      hr = m_pMadD3DDev->DrawPrimitive(D3DPT_TRIANGLEFAN, 0, 2);
    }
  }
  //Log("RenderTexture hr: 0x%08x", hr);
}

HRESULT MPMadPresenter::SetupOSDVertex(IDirect3DVertexBuffer9* pVertextBuf)
{
  VID_FRAME_VERTEX* vertices = nullptr;

  // Lock the vertex buffer
  HRESULT hr = pVertextBuf->Lock(0, 0, (void**)&vertices, D3DLOCK_DISCARD);

  if (SUCCEEDED(hr))
  {
    RECT rDest;
    rDest.bottom = m_dwHeight;
    rDest.left = m_dwLeft;
    rDest.right = m_dwWidth;
    rDest.top = m_dwTop;

    vertices[0].x = (float)rDest.left - 0.5f;
    vertices[0].y = (float)rDest.top - 0.5f;
    vertices[0].z = 0.0f;
    vertices[0].rhw = 1.0f;
    vertices[0].u = 0.0f;
    vertices[0].v = 0.0f;

    vertices[1].x = (float)rDest.right - 0.5f;
    vertices[1].y = (float)rDest.top - 0.5f;
    vertices[1].z = 0.0f;
    vertices[1].rhw = 1.0f;
    vertices[1].u = 1.0f;
    vertices[1].v = 0.0f;

    vertices[2].x = (float)rDest.right - 0.5f;
    vertices[2].y = (float)rDest.bottom - 0.5f;
    vertices[2].z = 0.0f;
    vertices[2].rhw = 1.0f;
    vertices[2].u = 1.0f;
    vertices[2].v = 1.0f;

    vertices[3].x = (float)rDest.left - 0.5f;
    vertices[3].y = (float)rDest.bottom - 0.5f;
    vertices[3].z = 0.0f;
    vertices[3].rhw = 1.0f;
    vertices[3].u = 0.0f;
    vertices[3].v = 1.0f;

    hr = pVertextBuf->Unlock();
    if (FAILED(hr))
      return hr;
  }

  return hr;
}

HRESULT MPMadPresenter::SetupOSDVertex3D(IDirect3DVertexBuffer9* pVertextBuf)
{
  VID_FRAME_VERTEX* vertices = nullptr;

  // Lock the vertex buffer
  HRESULT hr = pVertextBuf->Lock(0, 0, (void**)&vertices, D3DLOCK_DISCARD);

  if (SUCCEEDED(hr))
  {
    RECT rDest;
    rDest.bottom = m_dwHeightLeft;
    rDest.left = m_dwLeftLeft;
    rDest.right = m_dwWidthLeft;
    rDest.top = m_dwTopLeft;

    vertices[0].x = (float)rDest.left - 0.5f;
    vertices[0].y = (float)rDest.top - 0.5f;
    vertices[0].z = 0.0f;
    vertices[0].rhw = 1.0f;
    vertices[0].u = 0.0f;
    vertices[0].v = 0.0f;

    vertices[1].x = (float)rDest.right - 0.5f;
    vertices[1].y = (float)rDest.top - 0.5f;
    vertices[1].z = 0.0f;
    vertices[1].rhw = 1.0f;
    vertices[1].u = 1.0f;
    vertices[1].v = 0.0f;

    vertices[2].x = (float)rDest.right - 0.5f;
    vertices[2].y = (float)rDest.bottom - 0.5f;
    vertices[2].z = 0.0f;
    vertices[2].rhw = 1.0f;
    vertices[2].u = 1.0f;
    vertices[2].v = 1.0f;

    vertices[3].x = (float)rDest.left - 0.5f;
    vertices[3].y = (float)rDest.bottom - 0.5f;
    vertices[3].z = 0.0f;
    vertices[3].rhw = 1.0f;
    vertices[3].u = 0.0f;
    vertices[3].v = 1.0f;

    hr = pVertextBuf->Unlock();
    if (FAILED(hr))
      return hr;
  }

  return hr;
}

void MPMadPresenter::ReinitOSD(bool type)
{
  { 
    // Needed to update OSD/GUI when changing directx present parameter on resolution change.
    if (m_pReInitOSD)
    {
      if (type)
      {
        Log("%s : ReinitOSD from : ClearBackground", __FUNCTION__);
      }
      else
      {
        Log("%s : ReinitOSD from : RenderOsd", __FUNCTION__);
      }

      // Enable DisplayModeChanger is set by using DRR when player goes /leaves fullscreen (if we use profiles)
      EnableOriginalDisplayMode(true);

      m_pReInitOSD = false;
      m_pMPTextureGui = nullptr;
      m_pMPTextureOsd = nullptr;
      m_pMadGuiVertexBuffer = nullptr;
      m_pMadOsdVertexBuffer = nullptr;
      m_pRenderTextureGui = nullptr;
      m_pRenderTextureOsd = nullptr;
      m_hSharedGuiHandle = nullptr;
      m_hSharedOsdHandle = nullptr;
      m_pDevice->CreateTexture(m_dwGUIWidth, m_dwGUIHeight, 0, D3DUSAGE_RENDERTARGET, D3DFMT_A8R8G8B8, D3DPOOL_DEFAULT, &m_pMPTextureGui.p, &m_hSharedGuiHandle);
      m_pDevice->CreateTexture(m_dwGUIWidth, m_dwGUIHeight, 0, D3DUSAGE_RENDERTARGET, D3DFMT_A8R8G8B8, D3DPOOL_DEFAULT, &m_pMPTextureOsd.p, &m_hSharedOsdHandle);
      if (m_pMadD3DDev)
      {
        m_pMadD3DDev->CreateVertexBuffer(sizeof(VID_FRAME_VERTEX) * 4, D3DUSAGE_WRITEONLY, D3DFVF_VID_FRAME_VERTEX, D3DPOOL_DEFAULT, &m_pMadGuiVertexBuffer.p, NULL);
        m_pMadD3DDev->CreateVertexBuffer(sizeof(VID_FRAME_VERTEX) * 4, D3DUSAGE_WRITEONLY, D3DFVF_VID_FRAME_VERTEX, D3DPOOL_DEFAULT, &m_pMadOsdVertexBuffer.p, NULL);
        m_pMadD3DDev->CreateTexture(m_dwGUIWidth, m_dwGUIHeight, 0, D3DUSAGE_RENDERTARGET, D3DFMT_A8R8G8B8, D3DPOOL_DEFAULT, &m_pRenderTextureGui.p, &m_hSharedGuiHandle);
        m_pMadD3DDev->CreateTexture(m_dwGUIWidth, m_dwGUIHeight, 0, D3DUSAGE_RENDERTARGET, D3DFMT_A8R8G8B8, D3DPOOL_DEFAULT, &m_pRenderTextureOsd.p, &m_hSharedOsdHandle);
      }
      Log("%s : ReinitOSD for : %d x %d", __FUNCTION__, m_dwGUIWidth, m_dwGUIHeight);
    }
  }
}

void MPMadPresenter::ReinitD3DDevice()
{
  // Needed to release D3D device for resetting a new device from madVR
  try
  {
    if (m_pMPTextureGui)
    {
      m_pMPTextureGui.Release();
      m_pMPTextureGui = nullptr;
    }
    if (m_pMPTextureOsd)
    {
      m_pMPTextureOsd.Release();
      m_pMPTextureOsd = nullptr;
    }
    if (m_pMadGuiVertexBuffer)
    {
      m_pMadGuiVertexBuffer.Release();
      m_pMadGuiVertexBuffer = nullptr;
    }
    if (m_pMadOsdVertexBuffer)
    {
      m_pMadOsdVertexBuffer.Release();
      m_pMadOsdVertexBuffer = nullptr;
    }
    if (m_pRenderTextureGui)
    {
      m_pRenderTextureGui.Release();
      m_pRenderTextureGui = nullptr;
    }
    if (m_pRenderTextureOsd)
    {
      m_pRenderTextureOsd.Release();
      m_pRenderTextureOsd = nullptr;
    }
    
    //CloseHandle(m_hSharedGuiHandle);
    //CloseHandle(m_hSharedOsdHandle);
    m_hSharedGuiHandle = nullptr;
    m_hSharedOsdHandle = nullptr;
  }
  catch (...)
  {
    Log("%s : ReinitOSDDevice catch exception");
  }
  Log("%s : ReinitOSDDevice for : %d x %d", __FUNCTION__, m_dwGUIWidth, m_dwGUIHeight);
}

HRESULT MPMadPresenter::SetupMadDeviceState()
{
  HRESULT hr = E_UNEXPECTED;

  RECT newScissorRect;
  newScissorRect.bottom = m_dwHeight;
  newScissorRect.top = 0;
  newScissorRect.left = 0;
  newScissorRect.right = m_dwWidth;

  if (SUCCEEDED(hr = m_pMadD3DDev->SetScissorRect(&newScissorRect)))
    if (SUCCEEDED(hr = m_pMadD3DDev->SetVertexShader(NULL)))
      if (SUCCEEDED(hr = m_pMadD3DDev->SetFVF(D3DFVF_VID_FRAME_VERTEX)))
        if (SUCCEEDED(hr = m_pMadD3DDev->SetPixelShader(NULL)))
          if (SUCCEEDED(hr = m_pMadD3DDev->SetRenderState(D3DRS_ALPHABLENDENABLE, TRUE)))
            if (SUCCEEDED(hr = m_pMadD3DDev->SetRenderState(D3DRS_CULLMODE, D3DCULL_NONE)))
              if (SUCCEEDED(hr = m_pMadD3DDev->SetRenderState(D3DRS_LIGHTING, FALSE)))
                if (SUCCEEDED(hr = m_pMadD3DDev->SetRenderState(D3DRS_ZENABLE, FALSE)))
                  if (SUCCEEDED(hr = m_pMadD3DDev->SetRenderState(D3DRS_SRCBLEND, D3DBLEND_ONE)))
                    if (SUCCEEDED(hr = m_pMadD3DDev->SetRenderState(D3DRS_DESTBLEND, D3DBLEND_INVSRCALPHA)))
                      return hr;
  return hr;
}

HRESULT MPMadPresenter::SetDeviceOsd(IDirect3DDevice9* pD3DDev)
{
  {
    HRESULT hr = S_FALSE;

    if (m_pShutdown)
    {
      Log("MPMadPresenter::SetDeviceOsd() shutdown");
      return hr;
    }

    if (!pD3DDev)
    {
      if (m_pMadD3DDev != nullptr)
      {
        Log("MPMadPresenter::SetDeviceOsd() release m_pMadD3DDev");
        m_pMadD3DDev.Release();
        m_pMadD3DDev = nullptr;
      }
      return S_OK;
    }

    // Change madVR rendering D3D Device
    // if commented -> deadlock
    ChangeDevice(pD3DDev);

    if (m_pMadD3DDev && m_pCallback)
    {
      if (SUCCEEDED(hr = m_pDevice->CreateTexture(m_dwGUIWidth, m_dwGUIHeight, 0, D3DUSAGE_RENDERTARGET, D3DFMT_A8R8G8B8, D3DPOOL_DEFAULT, &m_pMPTextureGui.p, &m_hSharedGuiHandle)))
        if (SUCCEEDED(hr = m_pDevice->CreateTexture(m_dwGUIWidth, m_dwGUIHeight, 0, D3DUSAGE_RENDERTARGET, D3DFMT_A8R8G8B8, D3DPOOL_DEFAULT, &m_pMPTextureOsd.p, &m_hSharedOsdHandle)))
        {
          //=============================================
          // TODO disable OSD delay for now (used to force IVideoWindow on C# side)
          m_pCallback->ForceOsdUpdate(true);
          Log("%s : ForceOsdUpdate", __FUNCTION__);

          int frameCount = m_pCallback->ReduceFrame();
          Log("%s : reduce madVR frame to : %i", __FUNCTION__, frameCount);
          //=============================================
        }
      // Authorize OSD placement
      m_pReInitOSD = true;

      // Enable DisplayModeChanger is set by using DRR when player goes /leaves fullscreen
      EnableOriginalDisplayMode(true);
      return hr;
    }
    Log("MPMadPresenter::SetDeviceOsd() init madVR Window");
    return S_OK;
  }
}

STDMETHODIMP MPMadPresenter::ChangeDevice(IUnknown* pDev)
{
  CComQIPtr<IDirect3DDevice9Ex> pD3DDev = pDev;
  CheckPointer(pD3DDev, E_NOINTERFACE);

  HRESULT hr = S_FALSE;
  if (m_pMadD3DDev != pD3DDev)
  {
    m_pMadD3DDev = pD3DDev;
    return S_OK;
  }
  return hr;
}

HRESULT MPMadPresenter::SetDeviceSub(IDirect3DDevice9* pD3DDev)
{
  { 
    HRESULT hr = S_FALSE;

    if (m_pShutdown)
    {
      Log("MPMadPresenter::SetDeviceSub() shutdown");
      return hr;
    }

    // init or update madVR rendering D3D Device
    ChangeDevice(pD3DDev);

    if (m_pCallback)
    {
      m_pCallback->SetSubtitleDevice(reinterpret_cast<LONG>(pD3DDev));
      Log("MPMadPresenter::SetDeviceSub() send subtitle device to C# 0x:%x", pD3DDev);
      return S_OK;
    }
    return hr;
  }
}

HRESULT MPMadPresenter::Render(REFERENCE_TIME frameStart, int left, int top, int right, int bottom, int width, int height)
{
  return RenderEx(frameStart, 0, 0, left, top, right, bottom, width, height);
}

HRESULT MPMadPresenter::RenderEx(REFERENCE_TIME frameStart, REFERENCE_TIME frameStop, REFERENCE_TIME avgTimePerFrame, int left, int top, int right, int bottom, int width, int height)
{
  return RenderEx2(frameStart, frameStop, avgTimePerFrame, { left, top, right, bottom }, { left, top, right, bottom }, { 0, 0, width, height });
}

HRESULT MPMadPresenter::RenderEx2(REFERENCE_TIME frameStart, REFERENCE_TIME frameStop, REFERENCE_TIME avgTimePerFrame, RECT croppedVideoRect, RECT originalVideoRect, RECT viewportRect, const double videoStretchFactor /*= 1.0*/)
{
  return RenderEx3(std::move(frameStart), std::move(frameStop), std::move(avgTimePerFrame), std::move(croppedVideoRect), std::move(originalVideoRect), std::move(viewportRect), std::move(videoStretchFactor));
}

HRESULT MPMadPresenter::RenderEx3(REFERENCE_TIME rtStart, REFERENCE_TIME rtStop, REFERENCE_TIME atpf, RECT croppedVideoRect, RECT originalVideoRect, RECT viewportRect, const double videoStretchFactor /*= 1.0*/, int xOffsetInPixels /*= 0*/, DWORD flags /*= 0*/)
{
  if (m_pCallback)
  {
    if (m_pShutdown)
    {
      Log("%s : shutdown", __FUNCTION__);
      return S_FALSE;
    }

    SetupMadDeviceState();

    m_pCallback->RenderSubtitleEx(rtStart, viewportRect, croppedVideoRect, xOffsetInPixels);

    // Commented out but useful for testing
    //Log("%s : RenderSubtitle : rtStart: %i, croppedVideoRect.left: %d, croppedVideoRect.top: %d, croppedVideoRect.right: %d, croppedVideoRect.bottom: %d", __FUNCTION__, rtStart, croppedVideoRect.left, croppedVideoRect.top, croppedVideoRect.right, croppedVideoRect.bottom);
    //Log("%s : RenderSubtitle : viewportRect.right : %i, viewportRect.bottom : %i, xOffsetInPixels : %i", __FUNCTION__, viewportRect.right, viewportRect.bottom, xOffsetInPixels);
  }

  return S_OK;
}
