:: Checking if test should be run.
IF "%SC_RUN_SQL_REGRESSION_TEST%"=="False" GOTO :EOF

:: @ECHO OFF

:: clean up the database and processes
CMD /C kill_all.bat 2> NUL
IF EXIST .db (
RMDIR .db /S /Q
RMDIR .db.output /S /Q
)
IF EXIST SQLTest RMDIR SQLTest /S /Q
:: create the database
MKDIR .db
MKDIR .db.output
CMD /C sccreatedb.exe -ip .db SqlTest
:: weave the application
CMD /C scweaver.exe s\SQLTest\SQLTest.exe
IF %ERRORLEVEL% NEQ 0 (
:: clean up and exit code on fail
CMD /C kill_all.bat 2> NUL
REM RMDIR .db /S /Q
REM RMDIR .db.output /S /Q
REM RMDIR SQLTest /S /Q
ECHO Error: The regression test is failed!
EXIT /b 1
)

:: start servers
START scipcmonitor.exe PERSONAL .db.output
START scdata.exe SQLTEST SqlTest .db.output
START scdblog.exe SqlTest SqlTest .db.output
START 32bitComponents\scsqlparser.exe 8066
:: start the program

CALL sccode.exe SQLTEST --DatabaseDir=.db --OutputDir=.db.output --TempDir=.db.output --AutoStartExePath="s\SQLTest\.starcounter\SQLTest.exe" --FLAG:NoNetworkGateway
IF %ERRORLEVEL% NEQ 0 (
:: clean up and exit code on fail
CMD /C kill_all.bat 2> NUL
REM RMDIR .db /S /Q
REM RMDIR .db.output /S /Q
REM RMDIR SQLTest /S /Q
ECHO Error: The regression test is failed!
EXIT /b 1
) ELSE (
:: clean up and exit code on success
CMD /C kill_all.bat 2> NUL
REM RMDIR .db /S /Q
REM RMDIR .db.output /S /Q
REM RMDIR SQLTest /S /Q
ECHO Regression test succeeded.
EXIT /b 0
)
