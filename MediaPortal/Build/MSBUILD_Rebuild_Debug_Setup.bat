
@"%WINDIR%\Microsoft.NET\Framework\v4.0.30319\MSBUILD.exe" RestorePackages.targets /target:RestoreBuildPackages

set PathToBuildReport=.\..\Packages\BuildReport.1.0.0
xcopy /I /Y %PathToBuildReport%\_BuildReport_Files .\_BuildReport_Files

set xml=Build_Report_Debug_Setup.xml
set html=Build_Report_Debug_Setup.html

set logger=/l:XmlFileLogger,"%PathToBuildReport%\MSBuild.ExtensionPack.Loggers.dll";logfile=%xml%
"%WINDIR%\Microsoft.NET\Framework\v4.0.30319\MSBUILD.exe" /m Build.proj %logger% /property:OneStepOnly=true;BuildSetup=true;Configuration=Debug 

%PathToBuildReport%\msxsl %xml% _BuildReport_Files\BuildReport.xslt -o %html%
