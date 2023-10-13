for /f "delims=" %%t in ('adb shell pidof -s com.dd.TestAndroidFoldPhone') do set pid=%%t
echo %pid%
adb logcat --pid=%pid% -v time > D:/Logcat.log
pause