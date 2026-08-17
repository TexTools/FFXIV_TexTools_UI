@ECHO OFF
setlocal EnableExtensions

echo ==== Pushing update to BETA branch ====

if not exist FFXIV_TexTools.sln (
    echo TexTools.sln not found -- incorrect working directory.
    goto :fail
)

if "%~1"=="" (
    set /P patchver= Enter Version number. [In the form of 'v2.x.x.x']: 
) else (
    set patchver=%~1
)

if "%patchver%"=="" (
    echo Update cancelled.  No version number provided.
    goto :fail
)

echo.
echo Publishing %patchver% for Framework repo...
pause

pushd .\lib\xivmoddingframework || goto :fail
git checkout develop            || goto :popfail
call :publish                   || goto :popfail
popd

echo.
echo Publishing %patchver% for UI repo...
pause

REM Do not check out another branch in this repo while this script is running.
for /f %%b in ('git rev-parse --abbrev-ref HEAD') do set curbranch=%%b
if not "%curbranch%"=="develop" (
    echo UI repo is on '%curbranch%', expected 'develop'.  Check out develop and re-run.
    goto :fail
)

git add ./lib/*
git diff --cached --quiet
if errorlevel 1 (
    git commit -m "Update Framework Reference to Beta %patchver%" || goto :fail
) else (
    echo No framework reference change to commit.
)

call :publish || goto :fail

echo.
echo Done.  develop and beta both at %patchver%.  Still on develop in both repos.
pause
exit /b 0


REM --- publish current develop to origin/develop and origin/beta, plus tag ----
:publish
git tag -a %patchver% -m "Beta %patchver%" || exit /b 1
git push origin develop                    || exit /b 1
git push origin develop:beta               || exit /b 1
git push origin refs/tags/%patchver%       || exit /b 1
exit /b 0

:popfail
popd
:fail
echo.
echo *** ABORTED -- nothing further was pushed. ***
pause
exit /b 1