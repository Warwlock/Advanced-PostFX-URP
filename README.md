# Advanced-PostFX-URP
Advanced post-processing effects for URP.

Reminders
------------
This is experimental post processing effects from various sources to use in my URP projects. Check out the effects and requirements before including into your project.

Requirements
------------
- Unity 6000.0.58f2 (URP 17) or above.
- Some of the effetcs require Deferred rendering path, otherwise artifacts may occur.

Installation
-----------
- Open the **Package Manager** and select **Add package from git URL** from the add menu.
- Enter **https://github.com/Warwlock/Advanced-PostFX-URP.git** to install this package.
- If Unity could not find **git**, consider installing it [here](https://git-scm.com/downloads).
- Add the renderer features you want to use in your project into the active URP renderer.
- Add the override you want to use to the scene's Volume.

Effects
------------
- Auto Exposure
- Difference of Gaussians (Basic and Extended)
- Basic Edge Detection
- Object Outline based on object layer.
- Advanced Tone Mapping (**Custom3D** - Shclick - Ward - Reinhard - ReinhardExtended - Hable - Uchimura - NarkowiczACES - HillACES)

Documentation
------------
**https://github.com/Warwlock/Advanced-PostFX-URP/blob/main/Documentation~/Documentation.md**

References
------------

- [Acerola - Unity Post Processing Pipeline and Shaders](https://github.com/GarrettGunnell/Post-Processing)
- [MaxwellGengYF - Unity Ground Truth Ambient Occlusion](https://github.com/MaxwellGengYF/Unity-Ground-Truth-Ambient-Occlusion)
- [Unity Technologies - Auto Exposure from PostProcessing Stack V2](https://github.com/Unity-Technologies/PostProcessing)