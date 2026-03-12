@echo off
TITLE Sandwich Sector - Main
dotnet run --project Content.Server --configuration Release --config-file "bin/Content.Server/data/server_config.toml"
