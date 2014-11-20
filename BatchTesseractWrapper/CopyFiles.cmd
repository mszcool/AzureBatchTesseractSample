@echo off
echo "List Files for diagnostics..."
echo %WATASK_TVM_ROOT_DIR%
dir .\ /s
echo.
echo Moving BatchTesseractWrapper files to shared task directory
robocopy /MIR .\ %WATASK_TVM_ROOT_DIR%\shared
if "%errorlevel%" LEQ "4" (
   SET errorlevel=0
)