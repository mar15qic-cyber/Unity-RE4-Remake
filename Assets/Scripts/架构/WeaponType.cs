/// <summary>装备、弹药、背包映射及镜头配置共享的武器种类；数值作为兼容备弹索引，不能调整原顺序。</summary>
[UnityEngine.Scripting.APIUpdating.MovedFrom(true, "", "Assembly-CSharp", "WeaponType")]
public enum WeaponType
{
    /// <summary>中文含义：手枪。手枪，兼容备弹索引为零。</summary>
    Pistol,
    /// <summary>中文含义：步枪。步枪，兼容备弹索引为一。</summary>
    Rifle,
    /// <summary>中文含义：小刀。小刀，使用小刀战斗规则；不提供枪械备弹。</summary>
    Knife,
    /// <summary>中文含义：霰弹枪。霰弹枪，兼容备弹索引为三。</summary>
    Shotgun
}
