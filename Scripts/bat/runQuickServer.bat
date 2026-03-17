@echo off
cd ../../

call dotnet run --project Content.Server --no-build %* --config-file "bin/Content.Server/data/server_config.toml"
REM Sandwich: Check for server_config.toml file, and use if there for quick debugging with correct ccvar's

pause
