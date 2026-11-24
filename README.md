# Linus Arcade

A fullscreen Windows cabinet picker with an original pellet maze, five ghosts and four picture exits. Each pellet reappears in the same spot 10 seconds after you collect it. Getting caught sends the player back to the center. The maze fills the height of a 1920 × 1080 primary display. Either arcade stick or D-pad controls the same player. Arrow keys also work during setup. The player keeps moving until a wall, and you can queue the next turn before a junction.

## Install the games

[Download the latest Windows ZIP](https://github.com/Johann-Foerster/linus-arcade-gamepicker/releases/latest/download/LinusArcade.zip)

1. Run `build.cmd` on Windows 11 to create the two executables in `bin`, or extract the prebuilt cabinet ZIP. The build uses the .NET Framework compiler included with Windows. Place both executables in a permanent local folder such as `C:\Arcade`.
2. Sign into the Windows account used for cabinet autologin. Open `ArcadeSetup.exe` with a keyboard and mouse. Configure each exit with an application, optional arguments, working folder and thumbnail. Numbered setup tabs correspond to the four physical exit positions. The title is a setup reference. Game selection uses artwork alone.
3. Use an EXE or a normal Windows shortcut to an EXE. The picker keeps the shortcut's arguments and working folder. The setup arguments are appended. An explicit setup folder overrides the shortcut's folder. Quote individual paths containing spaces in the arguments field. For an emulator, select the emulator executable and supply the ROM path and fullscreen option as arguments.
4. Click **Save games** and start `ArcadePicker.exe`. Use the game or emulator's own fullscreen settings. The picker stays open underneath the game and resumes when the launched process exits, including an abnormal exit. Select the long-running game executable for launchers that hand off to another process.
5. Test both controllers, all four launches and game exits. **Alt+F4** on the picker opens the desktop for maintenance. Start the picker again after editing its setup.

Settings are stored in `%LOCALAPPDATA%\Linus Arcade\games.xml` for the cabinet user. Thumbnails and games are referenced at their original paths. A failed launch flashes the exit and returns the player to the center. The latest launch or library error is recorded in `last-error.txt` beside the settings.

## Exit a game with the controllers

1. Connect both controllers and open **Controllers** in `ArcadeSetup.exe`.
2. Click **Learn exit buttons**. Within 10 seconds, hold **Start** and **Select** (also labeled **Back** on some controllers) on both controllers until Setup confirms that it detected them. Click **Save games** and restart the picker. Repeat this setup if Windows changes the controllers' joystick IDs.
3. While a game is running, hold all four buttons at the same time for **2 continuous seconds**. Releasing any of them resets the countdown. The picker asks the game to close and returns to the maze when it exits. If the process has no closable main window or is still running after 3 seconds, the picker terminates it. A forced exit can lose unsaved progress.

## Start at sign-in on Windows 11 Pro

Use the built-in **Custom User Interface** user policy for the cabinet's autologin account. Windows starts the picker as that user's shell. This covers the session from sign-in onward. Firmware, the Windows boot logo, secure sign-in screens and Windows security shortcuts are controlled by Windows.

1. From an administrator account run `mmc.exe`. Open **File → Add/Remove Snap-in**, add **Group Policy Object Editor**, click **Browse → Users**, and select the specific local cabinet account. Finish the wizard.
2. In that user's **User Configuration → Administrative Templates → System**, open **Custom User Interface**. Enable it and enter the full path to the picker, for example `C:\Arcade\ArcadePicker.exe`.
3. Sign out and sign in as the cabinet user. Confirm the picker appears first, test a game returning to the maze, then check that Alt+F4 brings up Explorer. With cabinet autologin enabled, subsequent boots enter the same session automatically.

To restore normal desktop startup, use the administrator account and set the same user's **Custom User Interface** policy to **Not Configured**, then sign out of the cabinet account. Keep the installation path stable while the policy is active. Windows apps, startup utilities, update dialogs and driver overlays can draw their own windows. Check the final boot sequence with the cabinet's actual software and games.

[Microsoft's Custom User Interface policy reference](https://learn.microsoft.com/en-us/windows/client-management/mdm/policy-csp-admx-winlogon#customshell)

## Developer checks

To publish a release, push a new version tag such as `v0.2.0`. GitHub Actions runs the Windows build and checks, packages both executables with the instructions and license, and attaches `LinusArcade.zip` to the release.

Run `tests\check.cmd` on Windows. It builds both applications, checks maze reachability, gate triggers and repeated respawns, launches a real process directly and through a shortcut with parameters, renders the 1080p picker offscreen and constructs the setup form. Physical controller input, Windows sign-in and display transitions need an interactive cabinet test.
