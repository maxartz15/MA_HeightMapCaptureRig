using UnityEngine;
using MA_Toolbox.MA_Editor;

namespace MA_Toolbox.MA_HeightMapCaptureRig
{
    public class HeightMapCaptureRig : MonoBehaviour
    {
        [SerializeField]
        private Camera m_objectCamera = null;
        [SerializeField]
        private Camera m_terrainCamera = null;

        [Space]

        [SerializeField]
        private bool m_captureTerrainFromTop = true;
        [SerializeField]
        private float m_captureHeight = 100.0f;
        [SerializeField, Range(0, 1)]
        private float m_captureOffset = 0.5f;
        [SerializeField]
        private float m_captureSize = 32.0f;

        [Space]

        [SerializeField]
        private Resolution m_resolution = Resolution.m_256;
        public RenderTexture m_objectDepth { get; private set; }
        public RenderTexture m_terrainDepth { get; private set; }
        public RenderTexture m_objectDepthP { get; private set; }
        public RenderTexture m_heightMapRaw { get; private set; }
        public RenderTexture m_heightMapRemap { get; private set; }
        [SerializeField]
        private ComputeConfig m_heightMapComputeShader = new ComputeConfig();
        
        [Space]

        public float m_recoverRate = 0.1f;
        public float m_height = 1.0f;

        [Space]

        public MaterialOutput[] m_materials = null;

        //--------------------------------------------------------------

        private Vector3 m_prevPos = Vector3.zero;
        // offset.x, offset.y, resolution, recoveramount.
        private Vector4 m_params = new Vector4(0, 0, 0, 0);
        // height, captureHeight.
        private Vector4 m_params1 = new Vector4(0, 0, 0, 0);

        private void Start()
        {
            Setup();
        }

        private void Update()
        {
            SnapPosition();

            // Set shader offset.
            if(m_materials != null)
            {
                foreach (MaterialOutput m in m_materials)
                {
                    if(!string.IsNullOrWhiteSpace(m.m_offsetParameter))
                    {
                        m.m_material.SetVector(m.m_offsetParameter, new Vector4(this.transform.position.x * -1f, this.transform.position.z * -1f, 0, 0));
                    }
                }
            }

            // offset.x, offset.y, size, recoveramount.
            // Calculate compute offset.
            Vector3 offset = (m_prevPos - this.transform.position);
            float ppu = (float)m_resolution / m_captureSize;
            offset *= ppu;
            m_params.x = Mathf.Floor(offset.x);
            m_params.y = Mathf.Floor(offset.z);

            // Capture size.
            m_params.z = (int)m_resolution;
            // Height recovery.
            m_params.w = Time.deltaTime * m_recoverRate;

            // Height settings.
            m_params1.x = m_height;
            m_params1.y = m_captureHeight;

            // Capture terrain.
            m_objectCamera.Render();
            m_terrainCamera.Render();

            // Set values.
            m_heightMapComputeShader.m_computeShader.SetVector("_Params", m_params);
            m_heightMapComputeShader.m_computeShader.SetVector("_Params1", m_params1);

            // Run compute.
            m_heightMapComputeShader.Dispatch();

            // Save pos.
            m_prevPos = this.transform.position;
        }

        private void OnDestroy()
        {
            if(m_objectDepth != null)
            {
                m_objectDepth.Release();
                m_objectDepth = null;
            }

            if(m_terrainDepth != null)
            {
                m_terrainDepth.Release();
                m_terrainDepth = null;
            }

            if(m_objectDepthP != null)
            {
                m_objectDepthP.Release();
                m_objectDepthP = null;
            }

            if(m_heightMapRaw != null)
            {
                m_heightMapRaw.Release();
                m_heightMapRaw = null;
            }

            if(m_heightMapRemap != null)
            {
                m_heightMapRemap.Release();
                m_heightMapRemap = null;
            }
        }

        private void Setup()
        {
            m_prevPos = this.transform.position;

            SnapPosition();
            SetupRenderTextures();
            SetupCameras();
            SetupMaterials();
            SetupCompute();
        }

        private void SetupRenderTextures()
        {
            m_objectDepth = CreateDepthRenderTexture("ObjectDepth", m_resolution, false);
            m_objectDepthP = CreateDepthRenderTexture("PersistentObjectDepth", m_resolution, true, RenderTextureFormat.RFloat);
            m_terrainDepth = CreateDepthRenderTexture("TerrainDepth", m_resolution, false);
            m_heightMapRaw = CreateDepthRenderTexture("HeightMapRaw", m_resolution, true, RenderTextureFormat.Default);
            m_heightMapRemap = CreateDepthRenderTexture("HeightMapRemap", m_resolution, true, RenderTextureFormat.Default);
        }

        private void SetupCameras()
        {
            // Object camera.
            m_objectCamera.transform.localPosition = new Vector3(0, -m_captureHeight * m_captureOffset, 0);
            m_objectCamera.transform.rotation = Quaternion.Euler(-90, 0, 0);
            m_objectCamera.orthographicSize = m_captureSize / 2;
            m_objectCamera.farClipPlane = m_captureHeight;
            m_objectCamera.nearClipPlane = 0;
            m_objectCamera.targetTexture = m_objectDepth;

            // Terrain camera.
            if(m_captureTerrainFromTop)
            {
                m_terrainCamera.transform.localPosition = new Vector3(0, m_captureHeight * (1.0f - m_captureOffset), 0);
                m_terrainCamera.transform.rotation = Quaternion.Euler(90, 0, 0);
            }
            else
            {
                m_terrainCamera.transform.localPosition = new Vector3(0, -m_captureHeight * m_captureOffset, 0);
                m_terrainCamera.transform.rotation = Quaternion.Euler(-90, 0, 0);
            }
            m_terrainCamera.orthographicSize = m_captureSize / 2;
            m_terrainCamera.farClipPlane = m_captureHeight;
            m_terrainCamera.nearClipPlane = 0;
            m_terrainCamera.targetTexture = m_terrainDepth;
        }

        private void SetupMaterials()
        {
            if(m_materials != null)
            {
                foreach (MaterialOutput m in m_materials)
                {
                    if(!string.IsNullOrWhiteSpace(m.m_textureParameter))
                    {
                        switch (m.m_textureOutput)
                        {
                            case TextureOutput.m_none:
                                m.m_material.SetTexture(m.m_textureParameter, null);
                                break;
                            case TextureOutput.m_objectDepth:
                                m.m_material.SetTexture(m.m_textureParameter, m_objectDepth);
                                break;
                            case TextureOutput.m_terrainDepth:
                                m.m_material.SetTexture(m.m_textureParameter, m_terrainDepth);
                                break;
                            case TextureOutput.m_objectDepthP:
                                m.m_material.SetTexture(m.m_textureParameter, m_objectDepthP);
                                break;
                            case TextureOutput.m_heightMapRaw:
                                m.m_material.SetTexture(m.m_textureParameter, m_heightMapRaw);
                                break;
                            case TextureOutput.m_heightMapRemap:
                                m.m_material.SetTexture(m.m_textureParameter, m_heightMapRemap);
                                break;
                            default:
                                break;
                        }
                    }

                    if(!string.IsNullOrWhiteSpace(m.m_captureSizeParameter))
                    {
                        m.m_material.SetFloat(m.m_captureSizeParameter, m_captureSize);
                    }
                }
            }
        }

        private void SetupCompute()
        {
            // Get kernel.
            m_heightMapComputeShader.m_kerelId = m_heightMapComputeShader.m_computeShader.FindKernel("HeightMap");
            
            // Calculate thread group size.
            m_heightMapComputeShader.m_computeShader.GetKernelThreadGroupSizes(m_heightMapComputeShader.m_kerelId, out uint x1,  out uint y1,  out uint z1);
            m_heightMapComputeShader.m_tx = Mathf.CeilToInt((float)m_resolution / (int)x1);
            m_heightMapComputeShader.m_ty = Mathf.CeilToInt((float)m_resolution / (int)y1);
            m_heightMapComputeShader.m_tz = 1;

            // Set 'static' values.
            m_heightMapComputeShader.m_computeShader.SetBool("_CaptureFromTop", m_captureTerrainFromTop);
            m_heightMapComputeShader.m_computeShader.SetTexture(m_heightMapComputeShader.m_kerelId, "_ObjectDepth", m_objectDepth);
            m_heightMapComputeShader.m_computeShader.SetTexture(m_heightMapComputeShader.m_kerelId, "_TerrainDepth", m_terrainDepth);
            m_heightMapComputeShader.m_computeShader.SetTexture(m_heightMapComputeShader.m_kerelId, "_ObjectDepthP", m_objectDepthP);
            m_heightMapComputeShader.m_computeShader.SetTexture(m_heightMapComputeShader.m_kerelId, "_HeightMap", m_heightMapRaw);
            m_heightMapComputeShader.m_computeShader.SetTexture(m_heightMapComputeShader.m_kerelId, "_HeightMapRemap", m_heightMapRemap);
        }

        private RenderTexture CreateDepthRenderTexture(string a_name, Resolution a_resolution, bool a_enableRandomReadWrite, RenderTextureFormat a_renderTextureFormat = RenderTextureFormat.Depth)
        {
            RenderTexture renderTexture = new RenderTexture((int)a_resolution, (int)a_resolution, 24, a_renderTextureFormat);
            renderTexture.name = a_name;
            renderTexture.enableRandomWrite = a_enableRandomReadWrite;
            renderTexture.Create();

            return renderTexture;
        }

        private void SnapPosition()
        {
            float pws = (1.0f / (int)m_resolution) * m_captureSize;

            // Snap position to pixel.
            Vector3 newPos = Vector3.zero;
            newPos.x = (Mathf.Floor(this.transform.position.x / pws) + 0.5f) * pws;
            newPos.z = (Mathf.Floor(this.transform.position.z / pws) + 0.5f) * pws;
            this.transform.position = newPos;
        }

        private void OnValidate()
        {
            SnapPosition();
            SetupCameras();
            SetupMaterials();
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.white;
            Gizmos.DrawWireCube(this.transform.position + new Vector3(0, m_captureHeight * (.5f - m_captureOffset), 0), new Vector3(m_captureSize, m_captureHeight, m_captureSize));
        }
    }

    public enum Resolution
    {
        m_32 = 32,
        m_64 = 64,
        m_128 = 128,
        m_256 = 256,
        m_512 = 512,
        m_1024 = 1024,
        m_2048 = 2048,
        m_4096 = 4096,
        m_8192 = 8192
    }

    public enum TextureOutput
    {
        m_none = 0,
        m_objectDepth,
        m_terrainDepth,
        m_objectDepthP,
        m_heightMapRaw,
        m_heightMapRemap
    }

    [System.Serializable]
    public struct MaterialOutput
    {
        public Material m_material;
        public TextureOutput m_textureOutput;
        public string m_textureParameter;
        public string m_offsetParameter;
        public string m_captureSizeParameter;
    }

    [System.Serializable]
    public struct ComputeConfig
    {
        public ComputeShader m_computeShader;
        [ReadOnly]
        public int m_kerelId;
        [ReadOnly]
        public int m_tx;
        [ReadOnly]
        public int m_ty;
        [ReadOnly]
        public int m_tz;

        public void Dispatch()
        {
            m_computeShader.Dispatch(m_kerelId, m_tx, m_ty, m_tz);
        }
    }
}