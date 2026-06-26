using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace Proj3
{
    public class Proj3ExternalAnalyzer : MonoBehaviour
    {
        [SerializeField] private string analyzerUrl = "http://127.0.0.1:8000/analyze-scene";
        [SerializeField] private KeyCode analyzeKey = KeyCode.Y;
        [SerializeField] private bool mockService;
        [SerializeField] private StaticImageSource imageSource;
        [SerializeField] private Proj3PrototypeController controller;
        [SerializeField] private string outputJsonPath = "Proj3/segmentation.json";

        private bool isAnalyzing;

        private void Awake()
        {
            if (imageSource == null) imageSource = GetComponent<StaticImageSource>() ?? gameObject.AddComponent<StaticImageSource>();
            if (controller == null) controller = GetComponent<Proj3PrototypeController>();
        }

        private void Update()
        {
            if (Input.GetKeyDown(analyzeKey) && !isAnalyzing)
            {
                StartCoroutine(AnalyzeCurrentImage());
            }
        }

        public IEnumerator AnalyzeCurrentImage()
        {
            if (imageSource == null)
            {
                Debug.LogWarning("Proj3ExternalAnalyzer needs a StaticImageSource.");
                yield break;
            }

            Texture2D image = imageSource.GetImage();
            if (image == null)
            {
                Debug.LogWarning("Proj3ExternalAnalyzer could not get an image.");
                yield break;
            }

            isAnalyzing = true;
            byte[] png = image.EncodeToPNG();

            WWWForm form = new WWWForm();
            form.AddBinaryData("image", png, "scene.png", "image/png");
            form.AddField("mock", mockService ? "true" : "false");

            using (UnityWebRequest request = UnityWebRequest.Post(analyzerUrl, form))
            {
                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning("Proj3 analysis failed: " + request.error);
                    isAnalyzing = false;
                    yield break;
                }

                string outputPath = Path.Combine(Application.streamingAssetsPath, outputJsonPath);
                string directory = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllText(outputPath, request.downloadHandler.text);
                Debug.Log("Proj3 analysis saved to " + outputPath);

                if (controller != null)
                {
                    controller.Rebuild();
                }
            }

            isAnalyzing = false;
        }
    }
}
