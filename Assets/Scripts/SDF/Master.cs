using System.Collections.Generic;
using UnityEngine;


[ExecuteInEditMode, ImageEffectAllowedInSceneView]
public class Master : MonoBehaviour {
    public ComputeShader raymarching;

    public bool usesLiteMode = true; //might want to make 2 shaders and use the faster one then

    RenderTexture target;
    Camera cam;
    Light lightSource;
    List<ComputeBuffer> buffersToDispose;

    void Init () {
        cam = Camera.current;
        lightSource = FindObjectOfType<Light> ();
    }


    void OnRenderImage (RenderTexture source, RenderTexture destination) {
        Init ();
        buffersToDispose = new List<ComputeBuffer> ();

        InitRenderTexture ();
        CreateScene ();
        SetParameters ();

        raymarching.SetTexture (0, "Source", source);
        raymarching.SetTexture (0, "Destination", target);

        int liteModeDivider = usesLiteMode ? 16 : 1;

        int threadGroupsX = Mathf.CeilToInt (cam.pixelWidth / 16.0f / liteModeDivider);
        int threadGroupsY = Mathf.CeilToInt (cam.pixelHeight / 16.0f / liteModeDivider);
        raymarching.Dispatch (0, threadGroupsX, threadGroupsY, 1);

        Graphics.Blit (target, destination);

        foreach (var buffer in buffersToDispose) {
            buffer.Dispose ();
        }
    }

    void CreateScene () {
        List<Shape> allShapes = new List<Shape> (FindObjectsOfType<Shape> ());
        allShapes.Sort ((a, b) => a.operation.CompareTo (b.operation));

        List<Shape> orderedShapes = new List<Shape> ();

        for (int i = 0; i < allShapes.Count; i++) {
            // Add top-level shapes (those without a parent)
            if (allShapes[i].transform.parent == null) {

                Transform parentShape = allShapes[i].transform;
                orderedShapes.Add (allShapes[i]);
                allShapes[i].numChildren = parentShape.childCount;
                // Add all children of the shape (nested children not supported currently)
                for (int j = 0; j < parentShape.childCount; j++) {
                    if (parentShape.GetChild (j).GetComponent<Shape> () != null) {
                        orderedShapes.Add (parentShape.GetChild (j).GetComponent<Shape> ());
                        orderedShapes[orderedShapes.Count - 1].numChildren = 0;
                    }
                }
            }

        }

        ShapeData[] shapeData = new ShapeData[orderedShapes.Count];
        for (int i = 0; i < orderedShapes.Count; i++) {
            var s = orderedShapes[i];
            Vector3 col = new Vector3 (s.colour.r, s.colour.g, s.colour.b);
            shapeData[i] = new ShapeData () {
                position = s.Position,
                scale = s.Scale, 
                lightness = s.lightness,
                colour = col,
                shapeType = (int) s.shapeType,
                operation = (int) s.operation,
                blendStrength = s.blendStrength*3,
                numChildren = s.numChildren
            };
        }

        ComputeBuffer shapeBuffer = new ComputeBuffer (shapeData.Length, ShapeData.GetSize ());
        shapeBuffer.SetData (shapeData);
        raymarching.SetBuffer (0, "shapes", shapeBuffer);
        raymarching.SetInt ("numShapes", shapeData.Length);

        buffersToDispose.Add (shapeBuffer);
    }

    void SetParameters () {
        bool lightIsDirectional = lightSource.type == LightType.Directional;
        raymarching.SetMatrix ("_CameraToWorld", cam.cameraToWorldMatrix);
        raymarching.SetMatrix ("_CameraInverseProjection", cam.projectionMatrix.inverse);
        raymarching.SetVector ("_Light", (lightIsDirectional) ? lightSource.transform.forward : lightSource.transform.position);
        raymarching.SetBool ("positionLight", !lightIsDirectional);
        raymarching.SetFloat("crazyEffectStrength", 0f);
    }

    void InitRenderTexture () {
        if (target == null || target.width != cam.pixelWidth || target.height != cam.pixelHeight) {
            if (target != null) {
                target.Release ();
            }
            target = new RenderTexture (cam.pixelWidth, cam.pixelHeight, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            target.enableRandomWrite = true;
            target.Create ();
        }
    }



    struct ShapeData {
        public Vector3 position;
        public Vector3 scale;
        public Vector3 colour;
        public float lightness;
        public int shapeType;
        public int operation;
        public float blendStrength;
        public int numChildren;

        public static int GetSize () {
            return sizeof (float) * 11 + sizeof (int) * 3;
        }
    }
}
