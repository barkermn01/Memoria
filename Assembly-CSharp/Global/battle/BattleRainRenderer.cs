using Assets.Scripts.Common;
using FF9;
using Memoria.Scripts;
using System;
using System.Collections.Generic;
using UnityEngine;

public class BattleRainRenderer : MonoBehaviour
{
    private void Awake()
    {
        this.randSeed = -1;
        this.maxRain = 31;
        this.nf_BbgRainFlag = (Int32)FF9StateSystem.Common.FF9.btl_rain;
        if (this.nf_BbgRainFlag > this.maxRain)
        {
            this.nf_BbgRainFlag = this.maxRain;
        }

        // If you want a battle rain sound, set this to the appropriate SFX index.
        // Example: this.rainSfxIndex = FF9DBAll.SFX_BTL_SE020000; // replace with correct constant
        this.rainSfxIndex = -1;

        this.prevRainFlag = this.nf_BbgRainFlag;
        this.mat = ShadersLoader.CreateShaderMaterial("SPS/SPSRain");

        // Keep SFX lookup uninitialized until first rain event to avoid startup ordering issues.
        this.rainSfxInitialized = false;
        this.rainSfxSpecialEffectId = -1;
        this.rainSfxIndexInSpecialEffect = -1;
    }

    public void nf_BbgRain()
    {
        if (PersistenSingleton<SceneDirector>.Instance.IsFading)
        {
            return;
        }

        this.nf_BbgRainFlag = (Int32)FF9StateSystem.Common.FF9.btl_rain;
        if (this.nf_BbgRainFlag > this.maxRain)
        {
            this.nf_BbgRainFlag = this.maxRain;
        }

        // Start / stop rain SFX when flag transitions between 0 and non-zero.
        if (this.prevRainFlag == 0 && this.nf_BbgRainFlag > 0)
        {
            EnsureRainSfxInitialized();

            if (this.rainSfxIndexInSpecialEffect >= 0)
            {
                try
                {
                    // If we found a special-effect table, ensure it's loaded first.
                    if (this.rainSfxSpecialEffectId >= 0 && this.rainSfxSpecialEffectId != 0)
                    {
                        SoundLib.Log("[BattleRainRenderer] Loading SFX special effect id: " + this.rainSfxSpecialEffectId);
                        SoundLib.LoadSfxSoundData(this.rainSfxSpecialEffectId);
                    }

                    SoundLib.Log("[BattleRainRenderer] Playing rain SFX, indexInSpecialEffect: " + this.rainSfxIndexInSpecialEffect);
                    SoundLib.PlaySfxSound(this.rainSfxIndexInSpecialEffect, 1f, 0f, 1f);
                    this.isRainSoundPlaying = true;
                }
                catch (Exception ex)
                {
                    SoundLib.LogError("[BattleRainRenderer] PlaySfxSound failed: " + ex);
                }
            }
            else if (this.rainSfxIndex >= 0)
            {
                SoundLib.Log("[BattleRainRenderer] Playing legacy rainSfxIndex: " + this.rainSfxIndex);
                SoundLib.PlaySfxSound(this.rainSfxIndex, 1f, 0f, 1f);
                this.isRainSoundPlaying = true;
            }
            else
            {
                SoundLib.Log("[BattleRainRenderer] No rain SFX found to play.");
            }
        }
        else if (this.prevRainFlag > 0 && this.nf_BbgRainFlag == 0)
        {
            if (this.rainSfxIndexInSpecialEffect >= 0)
            {
                SoundLib.Log("[BattleRainRenderer] Stopping rain SFX indexInSpecialEffect: " + this.rainSfxIndexInSpecialEffect);
                SoundLib.StopSfxSound(this.rainSfxIndexInSpecialEffect);
                this.isRainSoundPlaying = false;
            }
            else if (this.rainSfxIndex >= 0)
            {
                SoundLib.Log("[BattleRainRenderer] Stopping legacy rainSfxIndex: " + this.rainSfxIndex);
                SoundLib.StopSfxSound(this.rainSfxIndex);
                this.isRainSoundPlaying = false;
            }
        }
        this.prevRainFlag = this.nf_BbgRainFlag;

        if (this.nf_BbgRainFlag == 0)
        {
            return;
        }
        GL.PushMatrix();
        this.mat.SetPass(0);
        GL.Begin(7);
        if ((FF9StateSystem.Battle.FF9Battle.attr & ff9btl.ATTR.NOPUTDISPENV) != 0)
        {
            if (this.randSeed == -1)
            {
                this.randSeed = UnityEngine.Random.seed;
            }
            else
            {
                UnityEngine.Random.seed = this.randSeed;
            }
        }
        else if (this.randSeed != -1)
        {
            this.randSeed = -1;
        }
        for (Int32 i = 0; i < this.nf_BbgRainFlag; i++)
        {
            Vector3 vector;
            vector.x = (Single)(((this._rand() & 511) - 256) * 41 / 32);
            vector.y = (Single)((this._rand() & 255) * 50 / 32 - 220);
            vector.z = (Single)(((this._rand() & 511) - 256) * 41 / 32);
            vector.x *= 10f;
            vector.y *= 10f;
            vector.z *= 10f;
            Vector3 end = vector;
            end.y += (Single)((85 + (this._rand() & 31)) * 10);
            vector.y *= -1f;
            end.y *= -1f;
            Color col = new Color32(25, 25, 50, Byte.MaxValue);
            Color col2 = new Color32(80, 80, 130, Byte.MaxValue);
            BattleRainRenderer.DrawBattleRain(vector, end, col, col2);
        }
        GL.End();
        GL.PopMatrix();
    }

    private Int32 _rand()
    {
        return UnityEngine.Random.Range(-4095, 4095);
    }

    public static void DrawBattleRain(Vector3 start, Vector3 end, Color col0, Color col1)
    {
        Vector3 lhs = Vector3.Cross(start, end);
        Vector3 a = Vector3.Cross(lhs, end - start);
        a.Normalize();
        Vector3 v = start + a * 5f;
        Vector3 v2 = start + a * -5f;
        Vector3 v3 = end + a * 5f;
        Vector3 v4 = end + a * -5f;
        GL.Color(col1);
        GL.Vertex(v4);
        GL.Color(col1);
        GL.Vertex(v3);
        GL.Color(col0);
        GL.Vertex(v);
        GL.Color(col0);
        GL.Vertex(v2);
    }

    // --- New SFX discovery state ---
    private void EnsureRainSfxInitialized()
    {
        if (this.rainSfxInitialized)
            return;

        this.rainSfxInitialized = true;

        try
        {
            // Ensure metadata is loaded if possible.
            SoundMetaData.LoadMetaData();
        }
        catch (Exception)
        {
            // Ignore - metadata may already be loaded or unavailable at this point.
        }

        try
        {
            // 1) Search resident SFX first
            try
            {
                List<String> resident = SoundMetaData.ResidentSfxSoundIndex[0];
                for (Int32 i = 0; i < resident.Count; i++)
                {
                    String path = resident[i];
                    if (path != null && path.IndexOf("rain", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        Debug.Log("[BattleRainRenderer] Found resident rain SFX: " + path + " at resident index " + i);
                        this.rainSfxSpecialEffectId = 0;
                        this.rainSfxIndexInSpecialEffect = i;
                        return;
                    }
                }
            }
            catch (Exception)
            {
                // continue searching special-effect tables
            }

            // 2) Search special-effect SFX tables
            foreach (KeyValuePair<Int32, List<String>> kv in SoundMetaData.SfxSoundIndex)
            {
                Int32 specialId = kv.Key;
                List<String> list = kv.Value;
                for (Int32 i = 0; i < list.Count; i++)
                {
                    String path = list[i];
                    if (path != null && path.IndexOf("rain", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        Debug.Log("[BattleRainRenderer] Found rain SFX in SfxSoundIndex: specialId=" + specialId + " index=" + i + " path=" + path);
                        this.rainSfxSpecialEffectId = specialId;
                        this.rainSfxIndexInSpecialEffect = i;
                        return;
                    }
                }
            }

            Debug.Log("[BattleRainRenderer] No rain SFX entry found in metadata.");
        }
        catch (Exception ex)
        {
            Debug.LogError("[BattleRainRenderer] EnsureRainSfxInitialized exception: " + ex);
        }
    }

    public Int32 maxRain;

    public Int32 nf_BbgRainFlag;

    private Material mat;

    private Int32 randSeed = -1;

    // Tracks previous frame flag to start/stop SFX on transition
    private Int32 prevRainFlag = 0;

    // Set this to the appropriate SFX index if you want a rain sound.
    // Use a defined FF9DBAll constant or a known sound index.
    private Int32 rainSfxIndex = -1;

    // New pair identifying the discovered rain SFX:
    // If rainSfxSpecialEffectId == 0 -> resident sound, index is rainSfxIndexInSpecialEffect
    // If rainSfxSpecialEffectId > 0 -> special-effect table, must call LoadSfxSoundData(specialEffectId) before PlaySfxSound(index)
    private Int32 rainSfxSpecialEffectId = -1;
    private Int32 rainSfxIndexInSpecialEffect = -1;
    private Boolean rainSfxInitialized = false;

    // Tracks whether we believe the rain SFX is playing.
    private Boolean isRainSoundPlaying = false;
}
