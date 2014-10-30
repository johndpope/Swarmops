@echo off
git submodule init
git submodule update

git submodule foreach git checkout master
git submodule foreach git pull

msbuild /p:Configuration=Release /t:Clean;Build NBitcoin\NBitcoin\NBitcoin.csproj > null

if exist bin erase /s /q bin > null
if not exist bin mkdir bin
cd bin
xcopy /s ..\NBitcoin\NBitcoin\bin\Release\* .
cd ..