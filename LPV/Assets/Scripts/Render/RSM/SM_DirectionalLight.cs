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
        //���������ͼ��ע������ʹ�õ���RenderTextureFormat.Depth���ͣ�ֱ��ʹ��Unity����������ͼ��ʽ
        shadowTexture = new RenderTexture(shadowResolution, shadowResolution, 24, RenderTextureFormat.Depth);
        shadowTexture.filterMode = FilterMode.Trilinear;
        lightCam = createLightCamera();
        //ָ���ƹ��������ȾĿ��
        lightCam.targetTexture = shadowTexture;
    }

    private void Update()
    {
        updateCamera(lightCam);
        //��ȾShadow Map
        lightCam.RenderWithShader(Shader.Find("LearningShadow/SMCaster"), "");
        //����Shadow Map
        Shader.SetGlobalTexture("_gShadowTexture", shadowTexture);

        //��������ת�ƹ����
        Matrix4x4 worldToLight = GL.GetGPUProjectionMatrix(lightCam.projectionMatrix, false) * lightCam.worldToCameraMatrix;
        //��������ת�ƹ����
        Shader.SetGlobalMatrix("_gWorldToLight", worldToLight);
        Shader.SetGlobalFloat("_gShadowBias", shadowBias);
    }

    private void updateCamera(Camera cam)
    {
        //��������İ�Χ��
        Vector3[] nearPt = new Vector3[4];
        Vector3[] farPt = new Vector3[4];
        //��ȡ���ü��桢Զ�ü���Ķ�������
        //�ú�����ȡ����������ֲ�����ϵ�Ķ�������
        Camera.main.CalculateFrustumCorners(new Rect(0, 0, 1, 1), Camera.main.nearClipPlane, Camera.MonoOrStereoscopicEye.Mono, nearPt);
        Camera.main.CalculateFrustumCorners(new Rect(0, 0, 1, 1), Camera.main.farClipPlane, Camera.MonoOrStereoscopicEye.Mono, farPt);
        //���������ֲ�����ϵת���ƹ�����ϵ�ľ���
        Matrix4x4 cameraToLight = transform.worldToLocalMatrix * Camera.main.transform.localToWorldMatrix;
        for (int i = 0; i < 4; ++i)
        {
            //����������ת�Ƶ��ƹ�����ϵ
            nearPt[i] = cameraToLight.MultiplyPoint(nearPt[i]);
            farPt[i] = cameraToLight.MultiplyPoint(farPt[i]);

            //Debug.DrawLine(transform.TransformPoint(nearPt[i]), transform.TransformPoint(farPt[i]), Color.red);
        }

        //�ҵ����е��������AABB��
        float[] xs = { nearPt[0].x, nearPt[1].x, nearPt[2].x, nearPt[3].x, farPt[0].x, farPt[1].x, farPt[2].x, farPt[3].x };
        float[] ys = { nearPt[0].y, nearPt[1].y, nearPt[2].y, nearPt[3].y, farPt[0].y, farPt[1].y, farPt[2].y, farPt[3].y };
        float[] zs = { nearPt[0].z, nearPt[1].z, nearPt[2].z, nearPt[3].z, farPt[0].z, farPt[1].z, farPt[2].z, farPt[3].z };

        Vector3 minPt = new Vector3(Mathf.Min(xs), Mathf.Min(ys), Mathf.Min(zs));
        Vector3 maxPt = new Vector3(Mathf.Max(xs), Mathf.Max(ys), Mathf.Max(zs));

        //������ת
        cam.transform.rotation = transform.rotation;
        //�����λ���ڽ����������
        cam.transform.position = transform.TransformPoint(new Vector3((minPt.x + maxPt.x) * 0.5f, (minPt.y + maxPt.y) * 0.5f, minPt.z));
        cam.nearClipPlane = 0;// minPt.z;
        cam.farClipPlane = maxPt.z - minPt.z;
        cam.aspect = (maxPt.x - minPt.x) / (maxPt.y - minPt.y);
        cam.orthographicSize = (maxPt.y - minPt.y) * 0.5f;
    }

    private Camera createLightCamera()
    {
        GameObject go = new GameObject("Directional Light Camera");
        //����ĳ������Դ����һ��
        go.transform.rotation = transform.rotation;
        //go.hideFlags = HideFlags.DontSave;

        Camera cam = go.AddComponent<Camera>();
        cam.backgroundColor = Color.white;
        cam.clearFlags = CameraClearFlags.Color;
        //ƽ�й�û��͸�ӹ�ϵ��ʹ���������ģ��ƽ�н��ܹ���
        cam.orthographic = true;
        cam.enabled = false;
        return cam;
    }
}

