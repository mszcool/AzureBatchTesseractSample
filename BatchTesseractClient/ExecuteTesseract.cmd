@echo off
echo Current Directory: 
dir .\ /s
echo.
echo Environment Variables SET with WA...
set WATASK_
echo.
echo Executing BatchTesseractWrapper.exe
echo - Blob Source:
echo   %1
echo - Target File:
echo   %2
echo.
echo -----------------------
echo Executing:
echo %WATASK_TVM_ROOT_DIR%\startup\wd\BatchTesseractWrapper.exe %1 %WATASK_TVM_ROOT_DIR%\%WATASK_WORKITEM_NAME%\%WATASK_JOB_NAME%\%WATASK_TASK_NAME%\wd\%2
echo.
%WATASK_TVM_ROOT_DIR%\startup\wd\BatchTesseractWrapper.exe %1 %WATASK_TVM_ROOT_DIR%\%WATASK_WORKITEM_NAME%\%WATASK_JOB_NAME%\%WATASK_TASK_NAME%\wd\%2
echo.
echo Exit Code: %errorlevel%
echo -----------------------
