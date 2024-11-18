Change Logs from v2.2.0:
- GUI: Fixed rom saving issues.
- GUI: Added dirty status display when file(s) are modified.

Extra Files Supported:
- `motion` folder - Containing UI/Sprites.
- `image` folder - Containing Background Images / misc.
- `text` folder - Containing Game Texts.
- `msg` folder - Containing Game Message Texts.

Note:
- For image, you are required to have both the corresponding *_arc.bin file and the target image file in order to work. You will only need to drag the folder containing these files to bulk extract them :)
- For music, you can use https://github.com/Kermalis/VGMusicStudio/releases/tag/v0.2.1 (Require the snd_data.sdat file in `etc` folder)
- For GUI version, decompression DO NOT export extra file other than the origin bin files.
- CLI and GUI version are standalone, they do not depend on each other. Although GUI version do require the dll/so/dylib files in order to run.

For CLI (fftool):
To use this program, simply drag the file / folder that you want to decompress into the `fftool` and it will do the job for you.

For GUI (fftoolgui):
Double click `fftoolgui.exe` or use the console to open the program `./fftoolgui`. Sorry in advance that linux and macOS users will need to use the console to open the program.