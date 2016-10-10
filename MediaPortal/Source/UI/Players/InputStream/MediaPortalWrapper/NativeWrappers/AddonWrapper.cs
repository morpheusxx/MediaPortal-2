using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace MediaPortalWrapper.NativeWrappers
{
  static class NativeMethods
  {
    [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Ansi)]
    public static extern IntPtr LoadLibrary([MarshalAs(UnmanagedType.LPStr)]string lpFileName);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Ansi)]
    public static extern IntPtr GetProcAddress(IntPtr hModule, string procedureName);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Ansi)]
    public static extern bool FreeLibrary(IntPtr hModule);
  }

  public enum AddonStatus
  {
    Ok,
    LostConnection,
    NeedRestart,
    NeedSettings,
    Unknown,
    NeedSavedSettings,
    PermanentFailure   /**< permanent failure, like failing to resolve methods */
  }

  [StructLayout(LayoutKind.Sequential, Pack=1)]
  public struct AddonStructSetting
  {
    public int Type;
    [MarshalAs(UnmanagedType.LPStr)]
    public string Id;
    [MarshalAs(UnmanagedType.LPStr)]
    public string Label;
    public int Current;
    //public string[] entry;
    public IntPtr Entry;
    public uint NumberOfEntries;
  }

  /*!
   * @brief Handle used to return data from the PVR add-on to CPVRClient
   */
  [StructLayout(LayoutKind.Sequential, Pack=1)]
  public struct AddonHandleStruct
  {
    public IntPtr CallerAddress;  /*!< address of the caller */
    public IntPtr DataAddress;    /*!< address to store data in */
    public int DataIdentifier; /*!< parameter to pass back when calling the callback */
  }

  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void GetAddonDlg(IntPtr pAddon);

  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate AddonStatus CreateDlg(ref AddonCB addonCb, IntPtr info);

  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void StopDlg();

  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void DestroyDlg();

  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate AddonStatus GetStatusDlg();

  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  [return: MarshalAs(UnmanagedType.I1)]
  public delegate bool HasSettingsDlg();

  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate uint GetSettingsDlg(IntPtr settings);

  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void FreeSettingsDlg();

  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate AddonStatus SetSettingDlg([MarshalAs(UnmanagedType.LPStr)]string settingName, IntPtr settingValue);


  [StructLayout(LayoutKind.Sequential, Pack=1)]
  public struct AddonCB
  {
    [MarshalAs(UnmanagedType.LPStr)]
    public string LibPath;
    public IntPtr addonData;
    public Delegate AddOnLib_RegisterMe;
    public Delegate AddOnLib_UnRegisterMe;
    public Delegate AudioEngineLib_RegisterMe;
    public Delegate AudioEngineLib_UnRegisterMe;
    public Delegate CodecLib_RegisterMe;
    public Delegate CodecLib_UnRegisterMe;
    public Delegate GUILib_RegisterMe;
    public Delegate GUILib_UnRegisterMe;
    public Delegate PVRLib_RegisterMe;
    public Delegate PVRLib_UnRegisterMe;
    public Delegate ADSPLib_RegisterMe;
    public Delegate ADSPLib_UnRegisterMe;
    public Delegate INPUTSTREAMLib_RegisterMe;
    public Delegate INPUTSTREAMLib_UnRegisterMe;
    public Delegate PeripheralLib_RegisterMe;
    public Delegate PeripheralLib_UnRegisterMe;
  }

  //<TStruct, TProps> : IDllAddon<TStruct, TProps>, 
  public class DllAddonWrapper<TFunc> : IDisposable where TFunc : new()
  {
    private IntPtr _pDll;

    public TFunc Addon
    {
      get
      {
        int sizeOfStruct = Marshal.SizeOf(typeof(TFunc));
        var ptrStruct = Marshal.AllocCoTaskMem(sizeOfStruct);
        GetAddon(ptrStruct);
        var res = (TFunc)Marshal.PtrToStructure(ptrStruct, typeof(TFunc));
        //Marshal.FreeCoTaskMem(ptrStruct);
        return res;
      }
    }

    public GetAddonDlg GetAddon { get; private set; }
    public CreateDlg Create { get; private set; }
    public StopDlg Stop { get; private set; }
    public DestroyDlg Destroy { get; private set; }
    public GetStatusDlg GetStatus { get; private set; }
    public HasSettingsDlg HasSettings { get; private set; }
    public GetSettingsDlg GetSettings { get; private set; }
    public FreeSettingsDlg FreeSettings { get; private set; }
    public SetSettingDlg SetSetting { get; private set; }

    public void Init(string addonDllPath)
    {
      _pDll = NativeMethods.LoadLibrary(addonDllPath);

      if (_pDll == IntPtr.Zero)
      {
        var lasterror = Marshal.GetLastWin32Error();
        var innerEx = new Win32Exception(lasterror);
        throw innerEx;
      }

      Dictionary<string, Func<IntPtr, bool>> initdll = new Dictionary<string, Func<IntPtr, bool>>
            {
                {"get_addon", fnPtr => { GetAddon = (GetAddonDlg) Marshal.GetDelegateForFunctionPointer(fnPtr, typeof (GetAddonDlg)); return true; } },
                {"ADDON_Create", fnPtr => { Create = (CreateDlg) Marshal.GetDelegateForFunctionPointer(fnPtr, typeof (CreateDlg)); return true; } },
                {"ADDON_Stop", fnPtr => { Stop = (StopDlg) Marshal.GetDelegateForFunctionPointer(fnPtr, typeof (StopDlg)); return true; } },
                {"ADDON_Destroy", fnPtr => { Destroy = (DestroyDlg) Marshal.GetDelegateForFunctionPointer(fnPtr, typeof (DestroyDlg)); return true; } },
                {"ADDON_GetStatus", fnPtr => { GetStatus = (GetStatusDlg) Marshal.GetDelegateForFunctionPointer(fnPtr, typeof (GetStatusDlg)); return true; } },
                {"ADDON_HasSettings", fnPtr => { HasSettings = (HasSettingsDlg) Marshal.GetDelegateForFunctionPointer(fnPtr, typeof (HasSettingsDlg)); return true; } },
                {"ADDON_GetSettings", fnPtr => { GetSettings = (GetSettingsDlg) Marshal.GetDelegateForFunctionPointer(fnPtr, typeof (GetSettingsDlg)); return true; } },
                {"ADDON_FreeSettings", fnPtr => { FreeSettings = (FreeSettingsDlg) Marshal.GetDelegateForFunctionPointer(fnPtr, typeof (FreeSettingsDlg)); return true; } },
                {"ADDON_SetSetting", fnPtr => { SetSetting = (SetSettingDlg) Marshal.GetDelegateForFunctionPointer(fnPtr, typeof (SetSettingDlg)); return true; } },
            };

      foreach (var func in initdll)
      {
        IntPtr pAddressOfFunctionToCall = NativeMethods.GetProcAddress(_pDll, func.Key);
        if (pAddressOfFunctionToCall == IntPtr.Zero)
          throw new InvalidOperationException();
        if (!func.Value(pAddressOfFunctionToCall))
          throw new InvalidOperationException();
      }
    }

    public void Dispose()
    {
      Stop();
      Destroy();
      if (_pDll != IntPtr.Zero)
        NativeMethods.FreeLibrary(_pDll);
    }
  }
}
