:: Run script from StarcounterBin!

:: Checking that Starcounter is properly installed.
IF NOT EXIST sccode.exe EXIT 1
IF NOT EXIST Personal.xml EXIT 2
IF "%StarcounterBin%"=="" EXIT 3

:: Creating repository if it does not exist.
IF NOT EXIST ".srv" star.exe @@CreateRepo .srv
COPY /Y scnetworkgateway.xml .srv\personal\scnetworkgateway.xml

:: Success message.
ECHO Tests preparation finished successfully!