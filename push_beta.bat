@ECHO OFF

echo ==== Pushing update to BETA branch ====
if not exist FFXIV_TexTools.sln (
	echo TexTools.sln not found -- Incorrect working directory.
	EXIT
)

if "%~1"=="" (
	SET /P patchver= Enter Version number. [In the form of 'v2.x.x.x']: 
) ELSE (
	set patchver=%1
)

if  %patchver%=="" (
	echo Update cancelled.  No version number provided.
	EXIT
)

echo Creating BETA update %patchver% for Framework Repo...
pause
cd ./lib/xivmoddingframework
git checkout beta
git merge develop --squash
git commit -m "Beta %patchver%"
git tag -a %patchver% -m "Beta %patchver%"
git push
git push --tags

echo Creating BETA update %patchver% for UI Repo...
pause
cd ../../
git checkout beta
git merge develop --squash
git add lib/*
git commit -m "Beta %patchver%"
git tag -a %patchver% -m "Beta %patchver%"
git push
git push --tags

echo Returning to Develop branch...
cd ./lib/xivmoddingframework
git checkout develop
rem git merge beta
git push

cd ../../
git checkout develop
rem git merge beta
git push

pause