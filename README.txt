STEREO ROUTER — DESKTOP AUDIO VERSION

Double-click StereoRouter-Pro.exe. Keep NAudio.dll in the same folder.
Requires Windows and .NET Framework 4.

ONE-TIME SETUP

Install VB-CABLE from https://vb-audio.com/Cable/ (the driver is required
to route system sound without creating feedback). Follow the vendor's
installation instructions and restart Windows if requested. In the app,
click Refresh devices after installation. Do not disable VB-CABLE's
loopback streaming option; it is enabled by default.

If VB-CABLE was already the Windows output before you start, the app will
use your selected left speaker as the output when you stop.

EVERYDAY USE

1. Choose the left and right monitors. Use Test left / Test right to
   identify them; swap the selections if necessary.
2. Click Start stereo. Desktop audio switches to the app automatically.
   Keep the app open while listening. Start at a low volume.
3. Adjust each speaker's volume independently from 0% to 200%, or use the
   combined slider to set both to the same level. If the individual levels
   differ, the combined slider displays their average. The ten EQ
   sliders change both speakers from -12 dB to +12 dB at each frequency.
   Choose Clean / less bass in the EQ preset menu for lighter bass, or
   try Flat, Voice clarity, and Warm / gentle. Adjusting individual bands
   makes a custom preset; your settings are remembered when you close.
   Settings are remembered when you close the app. Reset volume restores
   both volumes to 100% without changing EQ. Reset volumes + EQ also
   restores every EQ band to 0 dB.
4. Click Stop or close the app. The previous Windows output is restored,
   unless you changed it yourself while the app was running.

Volume above 100%, especially combined with positive EQ, can clip and
distort the signal. Reduce volume or EQ if it sounds distorted. Boosting
does not change the speakers' physical power limit.

The app saves the previous Windows output so it can recover on its next
launch if it was interrupted unexpectedly. If there is no sound after an
unexpected shutdown, reopen the app or choose an output in Windows Sound.

Apps configured to use a particular device instead of the Windows default
may bypass the router. Some running apps need restarting after the output
changes. Separate monitors use independent clocks, so there may be a small
delay or occasional interruption. Adjust monitor volumes if needed.

NAudio is distributed under the MIT license; see NAudio-LICENSE.txt.
