@echo off
cd apps\web
echo Running upload.test.tsx...
call npx jest src/__tests__/pages/upload.test.tsx --no-coverage --verbose
echo.
echo Test run complete!
pause