using UnityEditor;
using UnityEngine;

public class DebugRSM 
{


    [DrawGizmo(GizmoType.Selected)]      //当自身或者子物体被选中时，自动调用如下方法
    static void SelectedLight(Light light, GizmoType gizmoType)  //参数1为“XX”组件，可以随意选，参数2 必须写，不用赋值
    {
        Gizmos.color = Color.red;   //绘制时颜色
        DrawLightAABB(light);
    }


    private static void DrawAABB()
    {

        Gizmos.DrawWireCube(LPVFeature.Feature.FeatureSetting.bounds.center, LPVFeature.Feature.FeatureSetting.bounds.size);

    }


    private static void DrawLightAABB(Light light)
    {
        Camera camera = Camera.main;
        //计算相机的包围盒 camera space
        Vector3[] nearPt = new Vector3[4];
        Vector3[] farPt = new Vector3[4];
        //获取近裁剪面、远裁剪面的顶点坐标
        //该函数获取到的是相机局部坐标系的顶点坐标
        camera.CalculateFrustumCorners(new Rect(0, 0, 1, 1), camera.nearClipPlane, Camera.MonoOrStereoscopicEye.Mono, nearPt);
        camera.CalculateFrustumCorners(new Rect(0, 0, 1, 1), camera.farClipPlane, Camera.MonoOrStereoscopicEye.Mono, farPt);

        //计算相机的包围盒 world space
        Vector3[] nearPtWS = new Vector3[4];
        Vector3[] farPtWS = new Vector3[4];

        //计算从相机局部坐标系转到灯光坐标系的矩阵
        Matrix4x4 cameraMatrix = camera.transform.localToWorldMatrix;
        for (int i = 0; i < 4; ++i)
        {
            //将顶点坐标转移到灯光坐标系
            nearPtWS[i] = cameraMatrix.MultiplyPoint(nearPt[i]);
            farPtWS[i] = cameraMatrix.MultiplyPoint(farPt[i]);
        }

        var lightMatrix = light.transform.worldToLocalMatrix;
        var viewMatrix = light.transform.localToWorldMatrix;

        //计算相机的包围盒 camera space
        Vector3[] nearPtCS = new Vector3[4];
        Vector3[] farPtCS = new Vector3[4];

        for (int i = 0; i < 4; ++i)
        {
            //将顶点坐标转移到灯光坐标系
            nearPtCS[i] = lightMatrix.MultiplyPoint(nearPtWS[i]);
            farPtCS[i] = lightMatrix.MultiplyPoint(farPtWS[i]);
        }

        //在灯光坐标系下计算最大外包AABB盒
        float[] xs = { nearPtCS[0].x, nearPtCS[1].x, nearPtCS[2].x, nearPtCS[3].x, farPtCS[0].x, farPtCS[1].x, farPtCS[2].x, farPtCS[3].x };
        float[] ys = { nearPtCS[0].y, nearPtCS[1].y, nearPtCS[2].y, nearPtCS[3].y, farPtCS[0].y, farPtCS[1].y, farPtCS[2].y, farPtCS[3].y };
        float[] zs = { nearPtCS[0].z, nearPtCS[1].z, nearPtCS[2].z, nearPtCS[3].z, farPtCS[0].z, farPtCS[1].z, farPtCS[2].z, farPtCS[3].z };

        float minX = Mathf.Min(xs);
        float maxX = Mathf.Max(xs);
        float minY = Mathf.Min(ys);
        float maxY = Mathf.Max(ys);
        float minZ = Mathf.Min(zs);
        float maxZ = Mathf.Max(zs);


        Gizmos.color = Color.red;
        Vector3 previousNearCorners0 = new Vector3(minX, minY, minZ);
        Vector3 previousNearCorners2 = new Vector3(maxX, maxY, minZ);

        Vector3 pos = previousNearCorners0 + (previousNearCorners2 - previousNearCorners0) * 0.5f;

        pos = light.transform.TransformPoint(pos);
        
        viewMatrix.SetColumn(3, pos);
        viewMatrix.m33 = 1;
        lightMatrix = viewMatrix.inverse;


        for (int i = 0; i < 4; ++i)
        {
            //将顶点坐标转移到灯光坐标系
            nearPtCS[i] = lightMatrix.MultiplyPoint(nearPtWS[i]);
            farPtCS[i] = lightMatrix.MultiplyPoint(farPtWS[i]);
        }

        //在灯光坐标系下计算最大外包AABB盒
        float[] xs1 = { nearPtCS[0].x, nearPtCS[1].x, nearPtCS[2].x, nearPtCS[3].x, farPtCS[0].x, farPtCS[1].x, farPtCS[2].x, farPtCS[3].x };
        float[] ys1 = { nearPtCS[0].y, nearPtCS[1].y, nearPtCS[2].y, nearPtCS[3].y, farPtCS[0].y, farPtCS[1].y, farPtCS[2].y, farPtCS[3].y };
        float[] zs1 = { nearPtCS[0].z, nearPtCS[1].z, nearPtCS[2].z, nearPtCS[3].z, farPtCS[0].z, farPtCS[1].z, farPtCS[2].z, farPtCS[3].z };

        minX = Mathf.Min(xs1);
        maxX = Mathf.Max(xs1);
        minY = Mathf.Min(ys1);
        maxY = Mathf.Max(ys1);
        minZ = Mathf.Min(zs1);
        maxZ = Mathf.Max(zs1);

        Vector3 nearCorners0 = new Vector3(minX, minY, minZ);
        Vector3 nearCorners1 = new Vector3(maxX, minY, minZ);
        Vector3 nearCorners2 = new Vector3(maxX, maxY, minZ);
        Vector3 nearCorners3 = new Vector3(minX, maxY, minZ);

        Vector3 farCorners0 = new Vector3(minX, minY, maxZ);
        Vector3 farCorners1 = new Vector3(maxX, minY, maxZ);
        Vector3 farCorners2 = new Vector3(maxX, maxY, maxZ);
        Vector3 farCorners3 = new Vector3(minX, maxY, maxZ);


        nearCorners0 = viewMatrix.MultiplyPoint(nearCorners0);
        farCorners0 = viewMatrix.MultiplyPoint(farCorners0);
        nearCorners1 = viewMatrix.MultiplyPoint(nearCorners1);
        farCorners1 = viewMatrix.MultiplyPoint(farCorners1);
        nearCorners2 = viewMatrix.MultiplyPoint(nearCorners2);
        farCorners2 = viewMatrix.MultiplyPoint(farCorners2);
        nearCorners3 = viewMatrix.MultiplyPoint(nearCorners3);
        farCorners3 = viewMatrix.MultiplyPoint(farCorners3);


        Gizmos.color = Color.red;
        Gizmos.DrawLine(nearCorners0, nearCorners1);
        Gizmos.DrawLine(nearCorners1, nearCorners2);
        Gizmos.DrawLine(nearCorners2, nearCorners3);
        Gizmos.DrawLine(nearCorners3, nearCorners0);


        Gizmos.color = Color.green;
        Gizmos.DrawLine(farCorners0, farCorners1);
        Gizmos.DrawLine(farCorners1, farCorners2);
        Gizmos.DrawLine(farCorners2, farCorners3);
        Gizmos.DrawLine(farCorners3, farCorners0);

        Gizmos.DrawLine(nearCorners0, farCorners0);
        Gizmos.DrawLine(nearCorners1, farCorners1);
        Gizmos.DrawLine(nearCorners2, farCorners2);
        Gizmos.DrawLine(nearCorners3, farCorners3);

    }

}
