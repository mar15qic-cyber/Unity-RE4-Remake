# Unity-RE4-Remake

使用 Unity 6 制作的第三人称生存恐怖射击原型。公开内容为项目当前的玩法源码、Unity 配置、场景引用和简中对白资源。

## 已实现的内容

- 越肩瞄准、开火、换弹、武器切换、小刀、弹反和配对动作。
- 村民与电锯敌人、寻路、入村任务、鸣钟结尾及相关镜头表现。
- 公文包、物品检视、寄存箱、可改键输入和简中字幕。

## 工程入口

本项目使用 Unity `6000.0.76f1` 和内置渲染管线。正式入口是 `Assets/Scenes/TitleScreen.unity`，入村战斗位于 `Assets/RE4ChapterOneVillage/Lighting/VillageBattleLighting.unity`。玩法代码位于 `Assets/Scripts/`，编辑器工具和测试位于 `Assets/Editor/`。

公开版本适用于阅读源码和配置。完整运行需要原工程的模型、动画、贴图、场景烘焙资源、界面和音频，以及 Behavior Designer、Final IK、A* Pathfinding Project、Animancer、DOTween 等项目依赖。仓库保留了 Unity 引用和包配置，克隆后直接打开会遇到缺失引用。完整测试和构建应在资源齐全的工程中执行。

## 开发说明

- [玩法架构与模块调用链](Docs/架构改造/阶段一至六重构全览与模块调用链.md)
- [程序集边界与验收记录](Docs/架构改造/阶段七编译边界与验收记录.md)
- [战斗缺陷与回归记录](Docs/架构改造/阶段八战斗缺陷与回归记录.md)
- [村庄对白与字幕配置](Assets/RE4ChapterOneVillage/Resources/VillageDialogue/Lines.json)
