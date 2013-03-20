
:: Checking if test should be run.
IF "%SC_RUN_LOADANDLATENCY_TEST%"=="False" GOTO :EOF

:: Some predefined constants.
SET DB_DIR=.db
SET DB_OUT_DIR=.db.output
SET DB_NAME=LOADANDLATENCY
SET TEST_NAME=LoadAndLatency
SET TEST_ARGS=--UserArguments="SpecificTestType=2 %*"

:: Killing all processes.
CMD /C "kill_all.bat" 2>NUL

:: Checking for existing dirs.
IF EXIST .db (
RMDIR .db /S /Q
RMDIR .db.output /S /Q
)

:: Checking if directories exist.
IF NOT EXIST %DB_DIR% ( MKDIR %DB_DIR% )
IF NOT EXIST %DB_OUT_DIR% ( MKDIR %DB_OUT_DIR% )

:: Creating image files.
sccreatedb.exe -ip %DB_DIR% -lp %DB_DIR% %DB_NAME%

:: Starting IPC monitor first.
START CMD /C "scipcmonitor.exe PERSONAL %DB_OUT_DIR%"

:: Weaving the test.
scweaver.exe "s\%TEST_NAME%\%TEST_NAME%.exe"

:: Starting database memory management process.
START CMD /C "scdata.exe %DB_NAME% %DB_NAME% %DB_OUT_DIR%"

:: Sleeping some time using ping.
ping -n 3 127.0.0.1 > nul

:: Starting database with some delay.
sccode.exe %DB_NAME% --DatabaseDir=%DB_DIR% --OutputDir=%DB_OUT_DIR% --TempDir=%DB_OUT_DIR% --CompilerPath=MinGW\bin\x86_64-w64-mingw32-gcc.exe --AutoStartExePath=s\%TEST_NAME%\.starcounter\%TEST_NAME%.exe --FLAG:NoNetworkGateway %TEST_ARGS%