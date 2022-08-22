using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SM_DirectionalLight : MonoBehaviour
{
    public int shadowResolution = 1024;
    public float shadowBias = 0.005f;
    private RenderTexture shadowTexture;
    private Camera lightCam;
    private void Start()
    {
        //创建深度贴图，注意这里使用的是RenderTextureFormat.Depth类型，直接使用Unity定义的深度贴图格式
        shadowTexture = new RenderTexture(shadowResolution, shadowResolution, 24, RenderTextureFormat.Depth);
        shadowTexture.filterMode = FilterMode.Trilinear;
        lightCam = createLightCamera();
        //指定灯光相机的渲染目标
        lightCam.targetTexture = shadowTexture;
    }

    private void Update()
    {
        updateCamera(lightCam);
        //渲染Shadow Map
        lightCam.RenderWithShader(Shader.Find("LearningShadow/SMCaster"), "");
        //设置Shadow Map
        Shader.SetGlobalTexture("_gShadowTexture", shadowTexture);

        //计算世界转灯光矩阵
        Matrix4x4 worldToLight = GL.GetGPUProjectionMatrix(lightCam.projectionMatrix, false) * lightCam.worldToCameraMatrix;
        //设置世界转灯光矩阵
        Shader.SetGlobalMatrix("_gWorldToLight", worldToLight);
        Shader.SetGlobalFloat("_gShadowBias", shadowBias);
    }

    private void updateCamera(Camera cam)
    {
        //计算相机的包围盒
        Vector3[] nearPt = new Vector3[4];
        Vector3[] farPt = new Vector3[4];
        //获取近裁剪面、远裁剪面的顶点坐标
        //该函数获取到的是相机局部坐标系的顶点坐标
        Camera.main.CalculateFrustumCorners(new Rect(0, 0, 1, 1), Camera.main.nearClipPlane, Camera.MonoOrStereoscopicEye.Mono, nearPt);
        Camera.main.CalculateFrustumCorners(new Rect(0, 0, 1, 1), Camera.main.farClipPlane, Camera.MonoOrStereoscopicEye.Mono, farPt);
        //计算从相机局部坐标系转到灯光坐标系的矩阵
        Matrix4x4 cameraToLight = transform.worldToLocalMatrix * Camera.main.transform.localToWorldMatrix;
        for (int i = 0; i < 4; ++i)
        {
            //将顶点坐标转移到灯光坐标系
            nearPt[i] = cameraToLight.MultiplyPoint(nearPt[i]);
            farPt[i] = cameraToLight.MultiplyPoint(farPt[i]);

            //Debug.DrawLine(transform.TransformPoint(nearPt[i]), transform.TransformPoint(farPt[i]), Color.red);
        }

        //找到所有点的最大外包AABB盒
        float[] xs = { nearPt[0].x, nearPt[1].x, nearPt[2].x, nearPt[3].x, farPt[0].x, farPt[1].x, farPt[2].x, farPt[3].x };
        float[] ys = { nearPt[0].y, nearPt[1].y, nearPt[2].y, nearPt[3].y, farPt[0].y, farPt[1].y, farPt[2].y, farPt[3].y };
        float[] zs = { nearPt[0].z, nearPt[1].z, nearPt[2].z, nearPt[3].z, farPt[0].z, farPt[1].z, farPt[2].z, farPt[3].z };

        Vector3 minPt = new Vector3(Mathf.Min(xs), Mathf.Min(ys), Mathf.Min(zs));
        Vector3 maxPt = new Vector3(Mathf.Max(xs), Mathf.Max(ys), Mathf.Max(zs));

        //更新旋转
        cam.transform.rotation = transform.rotation;
        //相机的位置在近屏面的中心
        cam.transform.position = transform.TransformPoint(new Vector3((minPt.x + maxPt.x) * 0.5f, (minPt.y + maxPt.y) * 0.5f, minPt.z));
        cam.nearClipPlane = 0;// minPt.z;
        cam.farClipPlane = maxPt.z - minPt.z;
        cam.aspect = (maxPt.x - minPt.x) / (maxPt.y - minPt.y);
        cam.orthographicSize = (maxPt.y - minPt.y) * 0.5f;
    }

    private Camera createLightCamera()
    {
        GameObject go = new GameObject("Directional Light Camera");
        //相机的朝向与光源方向一致
        go.transform.rotation = transform.rotation;
        //go.hideFlags = HideFlags.DontSave;

        Camera cam = go.AddComponent<Camera>();
        cam.backgroundColor = Color.white;
        cam.clearFlags = CameraClearFlags.Color;
        //平行光没有透视关系，使用正交相机模拟平行接受光线
        cam.orthographic = true;
        cam.enabled = false;
        return cam;
    }
}

