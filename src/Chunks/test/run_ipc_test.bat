:: Checking if test should be run.
IF "%SC_RUN_IPC_TEST%"=="False" GOTO :EOF

staradmin -killall

:: Starting service in background.
START CMD /C "scservice.exe"

:: Waiting for service to initialize.
ping -n 10 127.0.0.1 > nul

SET TestParams=%*
IF "%TestParams%"=="" SET TestParams=PERSONAL 2 30000 2 administrator

:: Starting the client part of the test.
sc_ipc_test.exe %TestParams%

:: Checking exit code.
IF %ERRORLEVEL% NEQ 0 GOTO TESTFAILED

:: Success message.
ECHO IPC tests finished successfully!

staradmin -killall
GOTO :EOF

:: If we are here than some test has failed.
:TESTFAILED
ECHO Error occurred during the IPC test! 1>&2
staradmin -killall
EXIT 1