- 安卓设置resizeableActivity = false，则关闭多窗口模式，折叠时应用会重启。
  若resizeableActivity = true（默认值），则允许多窗口模式，只要没有在Unity里设置过分辨率（Screen.SetResolution），Unity会动态修改屏幕宽高。如果要完美适配，需要监听分辨率变化，从宽屏到窄屏，或从窄屏到宽屏时，UI需要重新布局（修改CanvasScaler的属性），**目前未确定最佳的实现方案**，1.安卓底层回调：onConfigurationChanged。2.Unity每帧去获取屏幕分辨率。
- 安卓设置resizeableActivity = false之后，出现某些手机无法全屏显示的问题，原因在于这些手机分辨率比例超过了安卓设置的max_aspect，所以调大max_aspect便能修复这个问题，建议值2.2。
- 参考：[Android 开发者  |  Android Developers (google.cn)](https://developer.android.google.cn/guide/topics/manifest/supports-screens-element?hl=zh-cn#compat-mode)