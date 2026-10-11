using Unity.AI.Navigation;
using UnityEngine;

namespace SectorLab.Tests
{
    // Builds a flat walkable ground with a NavMesh, so units with NavMeshAgent can be created on it.
    public static class TestArena
    {
        public static GameObject CreateGround(Transform parent, float size = 20f)
        {
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.SetParent(parent);
            ground.transform.localScale = new Vector3(size / 10f, 1f, size / 10f);
            ground.layer = LayerMask.NameToLayer("Ground");
            return ground;
        }

        public static GameObject CreateBlock(Transform parent, Vector3 position, Vector3 scale)
        {
            GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = "Block";
            block.transform.SetParent(parent);
            block.transform.position = position;
            block.transform.localScale = scale;
            return block;
        }

        // Bake after all static geometry exists and before any unit is created.
        public static void Bake(GameObject root)
        {
            Physics.SyncTransforms();
            var surface = root.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.layerMask = LayerMask.GetMask("Default", "Ground");
            surface.BuildNavMesh();
        }
    }
}
