using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
[UnityEngine.Scripting.APIUpdating.MovedFrom(true,"","Assembly-CSharp-Editor","村庄精修验收工具")]

public static class VillageRefinementValidationTool
{
    public static string CheckAsset()
    {
        var config=AssetDatabase.LoadAssetAtPath<InventoryPresentationConfiguration>("Assets/UI/重制界面/背包展示配置.asset");var report=new StringBuilder();int count=0;
        foreach(var guid in AssetDatabase.FindAssets("t:ItemData"))
        {
            var data=AssetDatabase.LoadAssetAtPath<ItemData>(AssetDatabase.GUIDToAssetPath(guid));var c=config.Find(data.itemId);count++;
            if (c == null || c.model == null || data.worldModelPrefab == null || data.inspectionModelPrefab != c.model)
            {
                throw new System.Exception("缺少物品模型或公文包与检视模型绑定不一致：" + data.displayName);
            }
            foreach(var renderer in c.model.GetComponentsInChildren<Renderer>())foreach(var mat in renderer.sharedMaterials)if(mat==null||mat.shader==null||mat.shader.name=="Hidden/InternalErrorShader")throw new System.Exception("物品材质异常："+data.displayName);
            int vertices=c.model.GetComponentsInChildren<MeshFilter>().Sum(f=>f.sharedMesh.vertexCount);
            foreach(var filter in c.model.GetComponentsInChildren<MeshFilter>())
                if(filter.sharedMesh.isReadable)foreach(var v in filter.sharedMesh.vertices)
                    if(float.IsNaN(v.x)||float.IsNaN(v.y)||float.IsNaN(v.z)||float.IsInfinity(v.sqrMagnitude))throw new System.Exception("模型含无效顶点："+data.displayName);
            report.AppendLine(data.displayName+"：公文包模型 "+vertices+" 顶点，"+c.model.GetComponentsInChildren<Renderer>().Length+" 个绘制部件");
        }
        report.AppendLine("总计："+count+"种");Directory.CreateDirectory("Temp/界面氛围验收");File.WriteAllText("Temp/界面氛围验收/资源检查.txt",report.ToString());return report.ToString();
    }
    public static string ModelScreenshot(string id,float yaw=0)
    {
        var config=AssetDatabase.LoadAssetAtPath<InventoryPresentationConfiguration>("Assets/UI/重制界面/背包展示配置.asset");var c=config.Find(id);var preview=new PreviewRenderUtility();
        bool old=ShaderUtil.allowAsyncCompilation;bool fog=RenderSettings.fog;
        Texture2D tex=null;
        try
        {
            ShaderUtil.allowAsyncCompilation=false;RenderSettings.fog=false;
            preview.BeginStaticPreview(new Rect(0,0,768,768));preview.camera.clearFlags=CameraClearFlags.SolidColor;preview.camera.backgroundColor=new Color(.065f,.073f,.08f);
            var obj=Object.Instantiate(c.model);preview.AddSingleGO(obj);obj.transform.rotation=Quaternion.Euler(c.orientation);
            var rr=obj.GetComponentsInChildren<Renderer>();var bounds=rr[0].bounds;foreach(var r in rr)bounds.Encapsulate(r.bounds);obj.transform.position=-bounds.center;
            obj.transform.RotateAround(Vector3.zero,Vector3.up,yaw);obj.transform.RotateAround(Vector3.zero,Vector3.right,8);
            preview.camera.orthographic=true;preview.camera.orthographicSize=Mathf.Max(bounds.extents.magnitude*1.13f,.1f);preview.camera.transform.position=new Vector3(0,0,-bounds.size.magnitude*3);preview.camera.transform.rotation=Quaternion.identity;preview.camera.nearClipPlane=.001f;preview.camera.farClipPlane=bounds.size.magnitude*8;
            preview.lights[0].transform.rotation=Quaternion.Euler(25,-35,0);preview.lights[0].intensity=1.4f;preview.lights[0].color=new Color(1,.97f,.91f);preview.lights[1].transform.rotation=Quaternion.Euler(-20,130,0);preview.lights[1].intensity=.65f;preview.ambientColor=new Color(.38f,.4f,.42f);
            preview.Render();tex=preview.EndStaticPreview();Directory.CreateDirectory("Temp/界面氛围验收/物品");string path="Temp/界面氛围验收/物品/"+id+"_"+yaw+".png";File.WriteAllBytes(path,tex.EncodeToPNG());return Path.GetFullPath(path);
        }
        finally{if(tex!=null)Object.DestroyImmediate(tex);preview.Cleanup();ShaderUtil.allowAsyncCompilation=old;RenderSettings.fog=fog;}
    }
}
