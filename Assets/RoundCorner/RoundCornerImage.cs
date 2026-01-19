using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Qjjxk.Round
{
    [ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(Graphic))]
    public class RoundCornerImage : MonoBehaviour
    {
        [SerializeField]
        private RoundType roundType;

        public RoundType RoundType
        {
            get => roundType;
            set
            {
                roundType = value;
                Refresh();
            }
        }

        /// true: Free false: Uniform
        [ToggleButton("Free", "Uniform")]
        public bool cornerType;

        [ShowIf("cornerType", false)]
        [SerializeField, Range(0, 1)]
        private float radius = 0.2f;

        public float Radius
        {
            get => radius;
            set
            {
                radius = Mathf.Clamp(value, 0, 1);
                Refresh();
            }
        }

        [SerializeField]
        [ShowIf("cornerType")]
        [VectorRange(0, 1, "LeftUp", "RightUp", "LeftDown", "RightDown")]
        private Vector4 freeRadius = Vector4.one * 0.2f;

        public Vector4 FreeRadius
        {
            get => freeRadius;
            set
            {
                freeRadius = Vector4.Max(Vector4.zero, Vector4.Min(value, Vector4.one));
                Refresh();
            }
        }

        [SerializeField, HideInInspector] private Shader shader;

        private static readonly int _radiusId = Shader.PropertyToID("_Radius");
        private static readonly int _freeRadiusId = Shader.PropertyToID("_FreeRadius");
        private static readonly int _roundTypeId = Shader.PropertyToID("_RoundType");
        private static readonly int _cornerTypeId = Shader.PropertyToID("_CornerType");
        private static readonly int _widthId = Shader.PropertyToID("_Width");
        private static readonly int _heightId = Shader.PropertyToID("_Height");
        private static readonly int _nId = Shader.PropertyToID("_N");
        private static readonly int _freeNId = Shader.PropertyToID("_FreeN");

        private const float C = -0.8f;
        private static readonly float _p2C = Mathf.Pow(2f, C);
        private static readonly float _p100C = Mathf.Pow(100f, C);

        private bool _isInit;
        private Material _material;
        private Graphic _graphic;
        private RectTransform _rectTransform;

        private void Awake()
        {
            if (_isInit) return;
            if (!shader) shader = Shader.Find("Qjjxk/RoundCorner");
            if (!shader) return;
            _graphic = GetComponent<Graphic>();
            if (_graphic is not (Image or RawImage)) return;
            _material = new Material(shader) { name = "RoundCorner" };
            _rectTransform = GetComponent<RectTransform>();
            _isInit = true;
            Refresh();
        }

        private void OnEnable()
        {
            if (_isInit) _graphic.material = _material;
        }

        private void Reset()
        {
            if (_isInit)
            {
                shader = Shader.Find("Qjjxk/RoundCorner");
                _graphic.material = _material;
                Refresh();
                return;
            }

            #if UNITY_EDITOR
            EditorUtility.DisplayDialog("Error",
                "The Shader was not found, or the script has not been added to the component containing the Imager or RawImage.", "OK");
            EditorApplication.delayCall += () => { DestroyImmediate(this); };
            #endif
        }

        private void OnValidate()
        {
            Refresh();
        }

        private void OnRectTransformDimensionsChange()
        {
            Refresh();
        }

        private void Refresh()
        {
            if (!_isInit) return;
            _material?.SetInt(_roundTypeId, (int)roundType);
            _material?.SetInt(_cornerTypeId, cornerType ? 1 : 0);
            _material?.SetFloat(_radiusId, radius);
            _material?.SetVector(_freeRadiusId, new Vector4(freeRadius.z, freeRadius.x, freeRadius.w, freeRadius.y));
            _material?.SetFloat(_nId, GetN(radius));
            _material?.SetVector(_freeNId, new Vector4(GetN(freeRadius.z), GetN(freeRadius.x), GetN(freeRadius.w), GetN(freeRadius.y)));
            _material?.SetFloat(_widthId, _rectTransform.rect.width);
            _material?.SetFloat(_heightId, _rectTransform.rect.height);
            
            return;

            float GetN(float tarRadius) => Mathf.Pow(tarRadius * (_p2C - _p100C) + _p100C, 1f / C);
        }

        private void OnDisable()
        {
            if (_isInit) _graphic.material = null;
        }
    }

    public enum RoundType
    {
        NormalRound,
        SuperEllipse
    }
}