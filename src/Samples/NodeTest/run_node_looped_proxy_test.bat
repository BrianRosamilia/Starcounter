@echo off
setlocal enabledelayedexpansion

:: Checking if number of cycles parameter is supplied.
set LOOP_TIMES=%1
IF "%LOOP_TIMES%"=="" SET LOOP_TIMES=100
ECHO Test is going to loop %LOOP_TIMES% times:

for /l %%x in (1, 1, %LOOP_TIMES%) do (

   :: Printing iteration number.
   ECHO %%x
   
   :: Running one node proxy test.
   run_node_proxy_test.bat
   
   :: Checking exit code.
   IF %ERRORLEVEL% NEQ 0 GOTO TESTFAILED
)

:: Success message.
ECHO Node loop proxy tests finished successfully!

staradmin -killall
GOTO :EOF

:: If we are here than some test has failed.
:TESTFAILED
ECHO Error occurred during the loop proxy test! 1>&2
staradmin -killall
EXIT 1