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

#include "stdafx.h"
#include <atlbase.h>

#include "Scheduler.h"
#include "IMVRCallback.h"
#include "IEVRCallback.h"
#include "D3DPresentEngine.h"
#include "mvrInterfaces.h"

using namespace std;

class MPMadPresenter : public CUnknown, public CCritSec
{
  class COsdRenderCallback : public CUnknown, public IOsdRenderCallback, public CCritSec
  {
    MPMadPresenter* m_pDXRAP;
    bool m_pShutdownOsd = false;

  public: COsdRenderCallback(MPMadPresenter* pDXRAP) : CUnknown(_T("COsdRender"), NULL) , m_pDXRAP(pDXRAP) {}

    DECLARE_IUNKNOWN
    STDMETHODIMP NonDelegatingQueryInterface(REFIID riid, void** ppv)
    {
      return (riid == __uuidof(IOsdRenderCallback)) ? GetInterface((IOsdRenderCallback*)this, ppv) :
        __super::NonDelegatingQueryInterface(riid, ppv);
    }

    void SetDXRAP(MPMadPresenter* pDXRAP)
    {
      m_pDXRAP = pDXRAP;
    }

    void SetShutdownOsd(bool pShutdownOsd)
    {
      m_pShutdownOsd = pShutdownOsd;
    }

    // IOsdRenderCallback

    STDMETHODIMP ClearBackground(LPCSTR name, REFERENCE_TIME frameStart, RECT *fullOutputRect, RECT *activeVideoRect)
    {
      if (m_pShutdownOsd)
      {
        return S_OK;
      }
      CAutoLock cAutoLock(this);
      return m_pDXRAP ? m_pDXRAP->ClearBackground(name, frameStart, fullOutputRect, activeVideoRect) : E_UNEXPECTED;
    }

    STDMETHODIMP RenderOsd(LPCSTR name, REFERENCE_TIME frameStart, RECT *fullOutputRect, RECT *activeVideoRect)
    {
      if (m_pShutdownOsd)
      {
        return S_OK;
      }
      CAutoLock cAutoLock(this);
      return m_pDXRAP ? m_pDXRAP->RenderOsd(name, frameStart, fullOutputRect, activeVideoRect) : E_UNEXPECTED;
    }

    STDMETHODIMP SetDevice(IDirect3DDevice9* pD3DDev)
    {
      { // Scope for autolock for the local variable (lock, which when deleted releases the lock)
        CAutoLock cAutoLock(this); // TODO fix possible deadlock on stop need to understand the situation
        Log("MPMadPresenterH::SetDeviceOsd() device 0x:%x", pD3DDev);
        if (m_pShutdownOsd)
        {
          if (!pD3DDev)
          {
            if (m_pDXRAP)
            {
              m_pDXRAP->ReinitD3DDevice(); // Can crash on D3D11 on stop
              m_pDXRAP->SetDeviceOsd(pD3DDev);
              // to see for deadlock needed to solve deadlock on stop
              m_pDXRAP = nullptr;
              Log("MPMadPresenterH::SetDeviceOsd() destroy");
            }
          }
          return S_OK;
        }

        if (!pD3DDev)
        {
          if (m_pDXRAP)
          {
            m_pDXRAP->ReinitD3DDevice();
            m_pDXRAP->SetDeviceOsd(pD3DDev);
            return S_OK;
          }
        }
        return m_pDXRAP ? m_pDXRAP->SetDeviceOsd(pD3DDev) : E_UNEXPECTED;
      }
    }
  };

  class CSubRenderCallback : public CUnknown, public ISubRenderCallback4, public CCritSec
  {
    MPMadPresenter* m_pDXRAPSUB;
    bool m_pShutdownSub = false;

    public: CSubRenderCallback(MPMadPresenter* pDXRAPSUB) : CUnknown(_T("CSubRender"), NULL) , m_pDXRAPSUB(pDXRAPSUB) {}

    DECLARE_IUNKNOWN
    STDMETHODIMP NonDelegatingQueryInterface(REFIID riid, void** ppv)
    {
      return (riid == __uuidof(ISubRenderCallback4)) ? GetInterface((ISubRenderCallback4*)this, ppv) :
        __super::NonDelegatingQueryInterface(riid, ppv);
    }

    void SetDXRAPSUB(MPMadPresenter* pDXRAPSUB)
    {
      m_pDXRAPSUB = pDXRAPSUB;
    }

    void SetShutdownSub(bool pShutdownSub)
    {
      m_pShutdownSub = pShutdownSub;
    }

    // ISubRenderCallback

    STDMETHODIMP SetDevice(IDirect3DDevice9* pD3DDev)
    {
      { // Scope for autolock for the local variable (lock, which when deleted releases the lock)
        CAutoLock cAutoLock(this); // TODO fix possible deadlock on stop need to understand the situation
        Log("MPMadPresenterH::SetDeviceSub() device 0x:%x", pD3DDev);
        if (m_pShutdownSub)
        {
          if (!pD3DDev)
          {
            if (m_pDXRAPSUB)
            {
              m_pDXRAPSUB->ReinitD3DDevice(); // Can crash on D3D11 on stop
              m_pDXRAPSUB->SetDeviceSub(pD3DDev);
              // to see for deadlock needed to solve deadlock on stop
              m_pDXRAPSUB = nullptr;
              Log("MPMadPresenterH::SetDeviceSub() destroy");
            }
          }
          return S_OK;
        }
        return m_pDXRAPSUB ? m_pDXRAPSUB->SetDeviceSub(pD3DDev) : E_UNEXPECTED;
      }
    }

    STDMETHODIMP Render(REFERENCE_TIME rtStart, int left, int top, int right, int bottom, int width, int height)
    {
      if (m_pShutdownSub)
      {
        return S_OK;
      }
      CAutoLock cAutoLock(this);
      return m_pDXRAPSUB ? m_pDXRAPSUB->Render(rtStart, left, top, right, bottom, width, height) : E_UNEXPECTED;
    }

    STDMETHODIMP RenderEx(REFERENCE_TIME frameStart, REFERENCE_TIME frameStop, REFERENCE_TIME avgTimePerFrame, int left, int top, int right, int bottom, int width, int height)
    {
      if (m_pShutdownSub)
      {
        return S_OK;
      }
      CAutoLock cAutoLock(this);
      return m_pDXRAPSUB ? m_pDXRAPSUB->Render(frameStart, left, top, right, bottom, width, height) : E_UNEXPECTED;
    }

    STDMETHODIMP RenderEx2(REFERENCE_TIME frameStart, REFERENCE_TIME frameStop, REFERENCE_TIME avgTimePerFrame, RECT croppedVideoRect, RECT originalVideoRect, RECT viewportRect, const double videoStretchFactor = 1.0)
    {
      if (m_pShutdownSub)
      {
        return S_OK;
      }
      CAutoLock cAutoLock(this);
      return m_pDXRAPSUB ? m_pDXRAPSUB->Render(frameStart, croppedVideoRect.left, croppedVideoRect.top, croppedVideoRect.right, croppedVideoRect.bottom, viewportRect.top, viewportRect.right) : E_UNEXPECTED;
    }

    STDMETHODIMP RenderEx3(REFERENCE_TIME frameStart, REFERENCE_TIME frameStop, REFERENCE_TIME avgTimePerFrame, RECT croppedVideoRect, RECT originalVideoRect, RECT viewportRect, const double videoStretchFactor = 1.0, int xOffsetInPixels = 0, DWORD flags = 0)
    {
      if (m_pShutdownSub)
      {
        return S_OK;
      }
      CAutoLock cAutoLock(this);
      return m_pDXRAPSUB ? m_pDXRAPSUB->RenderEx3(std::move(frameStart), std::move(frameStop), std::move(avgTimePerFrame), std::move(croppedVideoRect), std::move(originalVideoRect), std::move(viewportRect), std::move(videoStretchFactor), xOffsetInPixels) : E_UNEXPECTED;
    }
  };

  public:

    MPMadPresenter(IMVRCallback* pCallback, int xposition, int yposition, int width, int height, OAHWND parent, IDirect3DDevice9* pDevice, IGraphBuilder* pMediaControl);
    ~MPMadPresenter();

    void ConfigureMadvr();

    IBaseFilter* Initialize();
    void DeInitialize();
    void SetMadVrPaused(bool paused);
    void MadVrScreenResize(int x, int y, int width, int height, bool displayChange);
    void MadVr3D(bool Enable);

    HWND m_hWnd = nullptr;
    HINSTANCE m_hInstance = nullptr;
    #if !defined(NPT_POINTER_TO_LONG)
    #define NPT_POINTER_TO_LONG(_p) ((long)(_p))
    #endif

    STDMETHODIMP NonDelegatingQueryInterface(REFIID riid, void** ppv);
    STDMETHODIMP ClearBackground(LPCSTR name, REFERENCE_TIME frameStart, RECT *fullOutputRect, RECT *activeVideoRect);
    STDMETHODIMP RenderOsd(LPCSTR name, REFERENCE_TIME frameStart, RECT *fullOutputRect, RECT *activeVideoRect);
    STDMETHODIMP SetDeviceSub(IDirect3DDevice9* pD3DDev);
    STDMETHODIMP ChangeDevice(IUnknown* pDev);
    STDMETHODIMP SetDeviceOsd(IDirect3DDevice9* pD3DDev);
    // ISubRenderCallback
    STDMETHOD(Render)(REFERENCE_TIME frameStart, int left, int top, int right, int bottom, int width, int height);
    // ISubRenderCallback2
    STDMETHOD(RenderEx)(REFERENCE_TIME frameStart, REFERENCE_TIME frameStop, REFERENCE_TIME avgTimePerFrame, int left, int top, int right, int bottom, int width, int height);
    // ISubRenderCallback3
    STDMETHOD(RenderEx2)(REFERENCE_TIME frameStart, REFERENCE_TIME frameStop, REFERENCE_TIME avgTimePerFrame, RECT croppedVideoRect, RECT originalVideoRect, RECT viewportRect, const double videoStretchFactor = 1.0);
    // ISubRenderCallback4
    STDMETHOD(RenderEx3)(REFERENCE_TIME frameStart, REFERENCE_TIME frameStop, REFERENCE_TIME avgTimePerFrame, RECT croppedVideoRect, RECT originalVideoRect, RECT viewportRect, const double videoStretchFactor = 1.0, int xOffsetInPixels = 0, DWORD flags = 0);

    bool m_pShutdown = false;
    bool m_pReInitOSD = false;
    bool m_pPausedDone = false;
    bool m_pRunDone = false;
    int m_pPausedCount = 0;
    bool m_pPaused = false;
    IMVRCallback* m_pCallback = nullptr;

  private:
    void RenderToTexture(IDirect3DTexture9* pTexture);
    void RenderTexture(IDirect3DVertexBuffer9* pVertexBuf, IDirect3DTexture9* pTexture);

    HRESULT SetupOSDVertex(IDirect3DVertexBuffer9* pVertextBuf);
    HRESULT SetupOSDVertex3D(IDirect3DVertexBuffer9* pVertextBuf);
    void ReinitOSD(bool type);
    void ReinitD3DDevice();
    HRESULT SetupMadDeviceState();

    OAHWND m_hParent = reinterpret_cast<OAHWND>(nullptr);

    CComPtr<IDirect3DDevice9Ex> m_pDevice = nullptr;
    CComPtr<IDirect3DDevice9Ex> m_pMadD3DDev = nullptr;
    CComPtr<IGraphBuilder> m_pGraphbuilder = nullptr;
    CComPtr<IDirect3DSurface9> m_pSurfaceDevice = nullptr;
    CComPtr<IUnknown> m_pMad = nullptr;

    CComQIPtr<IDirect3DTexture9> m_pRenderTextureGui = nullptr;
    CComQIPtr<IDirect3DTexture9> m_pRenderTextureOsd = nullptr;
    CComQIPtr<IDirect3DTexture9> m_pMPTextureGui = nullptr;
    CComQIPtr<IDirect3DTexture9> m_pMPTextureOsd = nullptr;
    CComQIPtr<IDirect3DVertexBuffer9> m_pMadGuiVertexBuffer = nullptr;
    CComQIPtr<IDirect3DVertexBuffer9> m_pMadOsdVertexBuffer = nullptr;

    HANDLE m_hSharedGuiHandle = nullptr;
    HANDLE m_hSharedOsdHandle = nullptr;

    DWORD m_dwGUIWidth = 0;
    DWORD m_dwGUIHeight = 0;
    int m_Xposition = 0;
    int m_Yposition = 0;

    DWORD m_dwWidth = 0;
    DWORD m_dwHeight = 0;
    DWORD m_dwLeft = 0;
    DWORD m_dwTop = 0;

    DWORD m_dwWidthLeft = 0;
    DWORD m_dwHeightLeft = 0;
    DWORD m_dwLeftLeft = 0;
    DWORD m_dwTopLeft = 0;

    bool  m_madVr3DEnable = false;

    IOsdRenderCallback* m_pORCB = nullptr;
    ISubRenderCallback* m_pSRCB = nullptr;

    int m_ExclusiveMode = 0;
    int m_enableOverlay = 0;

    bool uiVisible = false;
};
