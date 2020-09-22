@ECHO OFF

echo ==== Pushing update to MASTER branch ====
if not exist FFXIV_TexTools.sln (
	echo TexTools.sln not found -- Incorrect working directory.
	EXIT
)

if "%~1"=="" (
	SET /P %1= Enter Version number. [In the form of 'v2.x.x.x']: 
	if  "%~1"=="" (
		echo Update cancelled.  No version number provided.
		EXIT
	)
)

echo Creating MASTER update %1 for Framework Repo...
cd ./lib/xivmoddingframework
git checkout master
git merge develop --squash
git commit -m "Update %1"
git tag -a %1 -m "Update %1"
git push
git push --tags

echo Creating MASTER update %1 for UI Repo...
cd ../../
git checkout master
git merge develop --squash
git add lib/*
git commit -m "Update %1"
git tag -a %1 -m "Update %1"
git push
git push --tags

echo Returning to Develop branch...
cd ./lib/xivmoddingframework
git checkout develop
cd ../../
git checkout develop

pause