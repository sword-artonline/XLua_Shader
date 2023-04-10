using UnityEngine;
using UnityEngine.SceneManagement;
using XLua;

namespace MyFluid
{
    [CSharpCallLua]
    struct FluidConfig
    {
        public int resolution;
        public float viscosity;
        public float force;
        public float exponent;

    }
    public class Fluid : MonoBehaviour
    {
        [SerializeField] private int _resolution = 512;
        [SerializeField] private float _viscosity = 1e-6f;
        [SerializeField] private float _force = 300;
        [SerializeField] private float _exponent = 200;
        [SerializeField] private Texture2D _init;

        [SerializeField] private ComputeShader _computeShader;
        [SerializeField] private Shader _shader;

        private Material _shaderMaterial;
        private Vector2 _previousInput;

        // Compute shader Kernels
        static class Kernels
        {
            public const int Advect = 0;
            public const int Force = 1;
            public const int PSetup = 2;
            public const int PFinish = 3;
            public const int Func1 = 4;
            public const int Func2 = 5;
        }

        #region compute shader ThreadCount
        private int ThreadCountX { get { return (_resolution + 7) / 8; } }
        private int ThreadCountY { get { return (_resolution * Screen.height / Screen.width + 7) / 8; } }
        #endregion

        private int ResolutionX { get { return ThreadCountX * 8; } }
        private int ResolutionY { get { return ThreadCountY * 8; } }

        // Vector field buffers
        static class VFB
        {
            public static RenderTexture Velocity1;
            public static RenderTexture Velocity2;
            public static RenderTexture Velocity3;
            public static RenderTexture Project1;
            public static RenderTexture Project2;
        }

        // Color buffers
        RenderTexture _colorRT1;
        RenderTexture _colorRT2;

        /// <summary>
        ///  Create renderTexture buffer
        /// </summary>
        /// <param name="format">RenderTexture Format</param>
        /// <param name="width">RenderTexture Width</param>
        /// <param name="height">RenderTexture Height</param>
        /// <returns></returns>
        RenderTexture AllocateBufferForRT(RenderTextureFormat format, int width = 0, int height = 0)
        {
            if (width == 0) width = ResolutionX;
            if (height == 0) height = ResolutionY;

            var rt = new RenderTexture(width, height, 0, format);
            rt.enableRandomWrite = true;
            rt.Create();
            return rt;
        }

        private void OnValidate()
        {
            _resolution = Mathf.Max(_resolution, 8);
        }

        void Start()
        {
            LoadResourceFromAB();

            ApplyLuaScriptConfig();

            InitRenderTexture();
        }

        void InitRenderTexture()
        {
            _shaderMaterial = new Material(_shader);

            VFB.Velocity1 = AllocateBufferForRT(RenderTextureFormat.RGHalf);
            VFB.Velocity2 = AllocateBufferForRT(RenderTextureFormat.RGHalf);
            VFB.Velocity3 = AllocateBufferForRT(RenderTextureFormat.RGHalf);
            VFB.Project1 = AllocateBufferForRT(RenderTextureFormat.RHalf);
            VFB.Project2 = AllocateBufferForRT(RenderTextureFormat.RHalf);

            _colorRT1 = AllocateBufferForRT(RenderTextureFormat.ARGBHalf, Screen.width, Screen.height);
            _colorRT2 = AllocateBufferForRT(RenderTextureFormat.ARGBHalf, Screen.width, Screen.height);

            Graphics.Blit(_init, _colorRT1);

        }

        void ApplyLuaScriptConfig()
        {
#if UNITY_EDITOR
            //In Editor: Config from Inspector 
#else
            //Config from Lua Script
            XLuaEnv.Instance.DoString("require('FluidShader')");
            LuaTable table = XLuaEnv.Instance.Global;
            FluidConfig config = table.Get<FluidConfig>("fluid");
            _resolution = config.resolution;
            _force = config.force;
            _exponent = config.exponent;
#endif
        }

        void LoadResourceFromAB()
        {
            AssetBundle file = AssetBundle.LoadFromFile(Config.ABPath + "/pic_unity");
            var sp = file.LoadAsset<Texture2D>("Img_unity");
            _init = sp;
            file.Unload(false);
        }

        void Update()
        {
            UpdateFluidEffect();
            if (Input.GetKeyDown(KeyCode.Space))
            {
                SceneManager.LoadScene("", LoadSceneMode.Single);
            }
        }

        private void UpdateFluidEffect()
        {
            float dt = Time.deltaTime;
            float dx = 1.0f / ResolutionY;

            //Input point
            Vector2 input = new Vector2(
                        (Input.mousePosition.x - Screen.width * 0.5f) / Screen.height,
                        (Input.mousePosition.y - Screen.height * 0.5f) / Screen.height);

            // Common variables
            _computeShader.SetFloat("Time", Time.time);
            _computeShader.SetFloat("DeltaTime", dt);

            // Advect
            _computeShader.SetTexture(Kernels.Advect, "U_in", VFB.Velocity1);
            _computeShader.SetTexture(Kernels.Advect, "W_out", VFB.Velocity2);
            _computeShader.Dispatch(Kernels.Advect, ThreadCountX, ThreadCountY, 1);

            // Diffuse setup
            float dif_alpha = dx * dx / (_viscosity * dt);
            _computeShader.SetFloat("Alpha", dif_alpha);
            _computeShader.SetFloat("Beta", 4 + dif_alpha);
            Graphics.CopyTexture(VFB.Velocity2, VFB.Velocity1);
            _computeShader.SetTexture(Kernels.Func2, "Y2_in", VFB.Velocity1);

            // Iteration
            for (int i = 0; i < 20; i++)
            {
                _computeShader.SetTexture(Kernels.Func2, "X2_in", VFB.Velocity2);
                _computeShader.SetTexture(Kernels.Func2, "X2_out", VFB.Velocity3);
                _computeShader.Dispatch(Kernels.Func2, ThreadCountX, ThreadCountY, 1);

                _computeShader.SetTexture(Kernels.Func2, "X2_in", VFB.Velocity3);
                _computeShader.SetTexture(Kernels.Func2, "X2_out", VFB.Velocity2);
                _computeShader.Dispatch(Kernels.Func2, ThreadCountX, ThreadCountY, 1);
            }

            // Add external force
            _computeShader.SetVector("ForceOrigin", input);
            _computeShader.SetFloat("ForceExponent", _exponent);
            _computeShader.SetTexture(Kernels.Force, "W_in", VFB.Velocity2);
            _computeShader.SetTexture(Kernels.Force, "W_out", VFB.Velocity3);

            if (Input.GetMouseButton(1))
                // Random push
                _computeShader.SetVector("ForceVector", Random.insideUnitCircle * _force * 0.025f);
            else if (Input.GetMouseButton(0))
                // Mouse drag
                _computeShader.SetVector("ForceVector", (input - _previousInput) * _force);
            else
                _computeShader.SetVector("ForceVector", Vector4.zero);

            _computeShader.Dispatch(Kernels.Force, ThreadCountX, ThreadCountY, 1);

            // Projection setup
            _computeShader.SetTexture(Kernels.PSetup, "W_in", VFB.Velocity3);
            _computeShader.SetTexture(Kernels.PSetup, "DivW_out", VFB.Velocity2);
            _computeShader.SetTexture(Kernels.PSetup, "P_out", VFB.Velocity1);
            _computeShader.Dispatch(Kernels.PSetup, ThreadCountX, ThreadCountY, 1);

            // Compute shader Func iteration
            _computeShader.SetFloat("Alpha", -dx * dx);
            _computeShader.SetFloat("Beta", 4);
            _computeShader.SetTexture(Kernels.Func1, "Y1_in", VFB.Velocity2);

            for (int i = 0; i < 20; i++)
            {
                _computeShader.SetTexture(Kernels.Func1, "X1_in", VFB.Project1);
                _computeShader.SetTexture(Kernels.Func1, "X1_out", VFB.Project2);
                _computeShader.Dispatch(Kernels.Func1, ThreadCountX, ThreadCountY, 1);

                _computeShader.SetTexture(Kernels.Func1, "X1_in", VFB.Project2);
                _computeShader.SetTexture(Kernels.Func1, "X1_out", VFB.Project1);
                _computeShader.Dispatch(Kernels.Func1, ThreadCountX, ThreadCountY, 1);
            }

            // Projection finish
            _computeShader.SetTexture(Kernels.PFinish, "W_in", VFB.Velocity3);
            _computeShader.SetTexture(Kernels.PFinish, "P_in", VFB.Project1);
            _computeShader.SetTexture(Kernels.PFinish, "U_out", VFB.Velocity1);
            _computeShader.Dispatch(Kernels.PFinish, ThreadCountX, ThreadCountY, 1);

            // Apply velocity field to the color buffer
            Vector2 offs = Vector2.one * (Input.GetMouseButton(1) ? 0 : 1e+7f);
            _shaderMaterial.SetVector("_ForceOrigin", input + offs);
            _shaderMaterial.SetFloat("_ForceExponent", _exponent);
            _shaderMaterial.SetTexture("_VelocityTex", VFB.Velocity1);
            Graphics.Blit(_colorRT1, _colorRT2, _shaderMaterial, 0);

            // Swap the color buffer
            var temp = _colorRT1;
            _colorRT1 = _colorRT2;
            _colorRT2 = temp;

            _previousInput = input;
        }

        private void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            Graphics.Blit(_colorRT1, destination, _shaderMaterial, 1);
        }

        private void OnDestroy()
        {
            Destroy(_shaderMaterial);

            Destroy(VFB.Velocity1);
            Destroy(VFB.Velocity2);
            Destroy(VFB.Velocity3);
            Destroy(VFB.Project1);
            Destroy(VFB.Project2);

            Destroy(_colorRT1);
            Destroy(_colorRT2);
        }

    }
}
