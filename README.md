# 战斗路线求解器

源码仓库：[Torch1230/CombatSolver](https://github.com/Torch1230/CombatSolver)。第三方代码来源与书面许可见 [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)。当前仓库已经公开源码；统一的软件许可证将在 Random Foreseer 后续许可证明确后补充，在此之前不要把公开可见误解为未受限制的复制、修改或再发布授权。

当前源码职责地图见 [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md)。开发过程、当前限制和未来路径预设构想见 [docs/DEVELOPMENT_NOTES.md](docs/DEVELOPMENT_NOTES.md)。逐项覆盖见 [docs/COMBAT_HOOK_COVERAGE.md](docs/COMBAT_HOOK_COVERAGE.md)，每个求解器自有适配的中文预期与测试闭环由开发者手工维护在 [docs/ADAPTATION_VERIFICATION.md](docs/ADAPTATION_VERIFICATION.md)，待测场景见 [docs/TEST_MATRIX.md](docs/TEST_MATRIX.md)。外部审计原文及处理结论见 [docs/AUDIT_REPORT.md](docs/AUDIT_REPORT.md) 与 [docs/AUDIT_RESPONSE.md](docs/AUDIT_RESPONSE.md)。

适配《杀戮尖塔 2》`0.111.0`。依赖：

- STS2-RitsuLib `0.5.13`

Combat Solver 的内置战斗模拟核心使用并改造了 Random Foreseer 的部分实现，现已获得原作者 hotwords123 的许可。相关来源关系持续存在于当前版本，涉及战斗状态、牌堆、RNG、Fork、History 和 Mirror 等基础逻辑；Combat Solver 在此基础上持续重构并扩展了跨回合搜索、路线复用、自动执行、原生选牌和性能控制。Combat Solver 不引用或部署 Random Foreseer 程序集，两者没有 manifest、程序集或补丁依赖；这项运行时分离不改变上述代码来源关系。完整本机构建使用 `pwsh -NoProfile -File tools\build-local-stack.ps1`。

开发回归可运行 `tools/run-unattended-test.ps1`。脚本默认直接启动隔离的塔 2 `--headless` 进程，使用独立 `APPDATA/LOCALAPPDATA`、关闭 Steam 并拒绝与交互式游戏进程并发。参数 `-VerifyIncrementalSearch` 会让每个候选同时执行增量分叉和旧完整回放并逐字段比较，仅用于正确性验证，不用于性能计时。`tools/run-visible-steam-benchmark.ps1` 通过 Steam 启动正常可见游戏，用固定机甲快照验证真实渲染进程的搜索耗时、GC 和帧间隔，并在测试后恢复请求/结果文件。
