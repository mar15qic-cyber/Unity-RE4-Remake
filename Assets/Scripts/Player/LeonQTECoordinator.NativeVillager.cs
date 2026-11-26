using System;
using RE4.ChapterOne;
using UnityEngine;

public partial class LeonQTECoordinator
{
    private static readonly int[] FrontGrabSeams = { 40, 95, 99, 115, 175, 186, 215, 235 };
    private const int FrontGrabInputFrame = 10;
    private const int FrontGrabDamageFrame = 75;
    // 0010 的第 91 帧仍为倒地，后续为存活者推开敌人并起身。
    private const int FrontGrabDownedFrame = 91;
    private const int AssassinationContactFrame = 57;

    private bool TryLoadNativeVillagerPair(QTEKind kind, IEnemyQTEParticipant target, out NativePairPlayback playback)
    {
        playback = null;
        if (kind is not (QTEKind.FrontGrab or QTEKind.RearBiteNeck or QTEKind.KnifeAssassination)
            || playerAnimator == null || playerAnimator.isHuman || target.Animator == null || target.Animator.isHuman
            || target is not VillagerQTEParticipant villager || villager.controller == null)
        {
            return false;
        }

        MissionEnemyType enemyType = villager.controller.missionEnemyType;
        if (enemyType == MissionEnemyType.Sawman)
        {
            if (kind != QTEKind.KnifeAssassination)
            {
                return false;
            }
        }
        else if (enemyType != MissionEnemyType.Ganado)
        {
            return false;
        }

        var animatorController = target.Animator.runtimeAnimatorController;
        if (animatorController == null)
        {
            return false;
        }

        // 主场景男女与持械模板使用不同的原版骨架，电锯人从 SawKnifeFollowUp 读取已烘焙的 SalvadorPair 轨道。
        string role = string.Empty;
        if (enemyType == MissionEnemyType.Sawman)
        {
            role = "Salvador";
        }
        else
        {
            switch (animatorController.name)
            {
                case "MainGanado":
                    role = "Ganado";
                    break;
                case "MainGanadoMale":
                case "MainGanadoArmed":
                    role = "GanadoMale";
                    break;
                default:
                    return false;
            }
        }

        var bank = Resources.Load<ChapterOnePairMotion>(ChapterOnePairMotion.ResourceName);
        if (bank == null)
        {
            return false;
        }

        ChapterOnePairMotion enemyBank = bank;
        string enemyPrefix = "GanadoPair_";
        if (enemyType == MissionEnemyType.Sawman)
        {
            enemyBank = Resources.Load<ChapterOnePairMotion>("SawKnifeFollowUp");
            if (enemyBank == null)
            {
                return false;
            }

            enemyPrefix = "SalvadorPair_";
        }

        if (kind == QTEKind.KnifeAssassination)
        {
            string motion = "1101";
            int end = 295;
            if (!assassinationTriggeredFromStagger && controller.IsCrouching)
            {
                motion = "1105";
                end = 305;
            }
            if (!TryReadStage(motion, end, out var stab))
            {
                return false;
            }
            playback = new NativePairPlayback(stab, default, default, 0, AssassinationContactFrame,
                Array.Empty<int>(), true);
            playback.HoldEnemyResultPose = true;
        }
        else if (kind == QTEKind.RearBiteNeck)
        {
            const int inputFrame = 26;
            int deadline = Mathf.Clamp(inputFrame + Mathf.RoundToInt(config.inputDuration * 60f), inputFrame + 1, 490);
            if (!TryReadStage("0051", deadline, out var hold)
                || !TryReadStage("0070", 162, out var escape)
                || !TryReadStage("0051", deadline + 80, out var timeout))
            {
                return false;
            }
            // 0051 是连续抓持。超时保持同一条根轨，避免配对角色重置到进入姿势。
            playback = new NativePairPlayback(hold, escape, timeout, inputFrame, deadline + 22,
                new[] { 48, 76, 106, 137, 168, 199, 232, 260, 294, 325, 355, 389, 421, 450, 477 });
            playback.HoldEnemyResultPose = true;
        }
        else
        {
            int deadline = Mathf.Clamp(FrontGrabInputFrame + Mathf.RoundToInt(config.inputDuration * 60f), 11, 467);
            if (!TryReadStage("0000", deadline, out var hold)
                || !TryReadStage("0020", 190, out var escape)
                || !TryReadStage("0010", 255, out var bite))
            {
                return false;
            }
            playback = new NativePairPlayback(hold, escape, bite, FrontGrabInputFrame,
                FrontGrabDamageFrame, FrontGrabSeams);
        }
        return playback.HasValidStages;

        bool TryReadStage(string motion, int endFrame, out NativePairPlayback.Stage stage)
        {
            stage = default;
            if (!bank.TryGetVillagerTrack("Leon", "LeonPair_" + motion, out var playerTrack)
                || !enemyBank.TryGetVillagerTrack(role, enemyPrefix + motion, out var enemyTrack))
            {
                return false;
            }

            stage = new NativePairPlayback.Stage(playerTrack, enemyTrack, endFrame);
            return stage.IsValid;
        }
    }
}

