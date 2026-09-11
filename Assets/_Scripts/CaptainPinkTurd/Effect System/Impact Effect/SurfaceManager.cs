using System;
using System.Collections.Generic;
using CaptainPinkTurd.AudioSystem;
using CaptainPinkTurd.Core.DesignPattern.Singleton;
using CaptainPinkTurd.Core.Extensions;
using CaptainPinkTurd.Core.Utilities;
using UnityEngine;
using Random = UnityEngine.Random;

namespace CaptainPinkTurd.EffectSystem.ImpactEffect
{
    public class SurfaceManager : Singleton<SurfaceManager>
    {
        [SerializeField] private List<SurfaceType> surfaces = new List<SurfaceType>();
        [SerializeField] private int defaultPoolSizes = 10;
        [SerializeField] private Surface defaultSurface;

        private SoundBuilder soundBuilder;

        private void Start()
        {
            soundBuilder = SoundManager.Instance.CreateSoundBuilder();
        }

        public void HandleImpact(GameObject hitObject, Vector3 hitPoint, Vector3 hitNormal, ImpactType impact,
            int triangleIndex)
        {
            if (hitObject.TryGetComponent(out Terrain terrain))
            {
                List<TextureAlpha> activeTextures = GetActiveTexturesFromTerrain(terrain, hitPoint);
                foreach (TextureAlpha activeTexture in activeTextures)
                {
                    SurfaceType surfaceType = surfaces.Find(surface => surface.albedo == activeTexture.Texture);
                    if (surfaceType != null)
                    {
                        foreach (Surface.SurfaceImpactTypeEffect typeEffect in surfaceType.surface.impactTypeEffects)
                        {
                            if (typeEffect.impactType == impact)
                            {
                                PlayEffects(hitPoint, hitNormal, typeEffect.surfaceEffect, hitObject);
                            }
                        }
                    }
                    else
                    {
                        foreach (Surface.SurfaceImpactTypeEffect typeEffect in defaultSurface.impactTypeEffects)
                        {
                            if (typeEffect.impactType == impact)
                            {
                                PlayEffects(hitPoint, hitNormal, typeEffect.surfaceEffect, hitObject);
                            }
                        }
                    }
                }
            }
            else if (hitObject.transform.TryGetComponentInHierarchy(out Renderer rend))
            {
                Texture activeTexture = GetActiveTextureFromRenderer(rend, triangleIndex);

                SurfaceType surfaceType = surfaces.Find(surface => surface.albedo == activeTexture);
                if (surfaceType != null)
                {
                    foreach (Surface.SurfaceImpactTypeEffect typeEffect in surfaceType.surface.impactTypeEffects)
                    {
                        if (typeEffect.impactType == impact)
                        {
                            PlayEffects(hitPoint, hitNormal, typeEffect.surfaceEffect, hitObject);
                        }
                    }
                }
                else
                {
                    foreach (Surface.SurfaceImpactTypeEffect typeEffect in defaultSurface.impactTypeEffects)
                    {
                        if (typeEffect.impactType == impact)
                        {
                            PlayEffects(hitPoint, hitNormal, typeEffect.surfaceEffect, hitObject);
                        }
                    }
                }
            }
            else
            {
                Debug.LogError($"Couldn't find a surface to handle impact for {hitObject.name}");
            }
        }

        private List<TextureAlpha> GetActiveTexturesFromTerrain(Terrain terrain, Vector3 hitPoint)
        {
            Vector3 terrainPosition = hitPoint - terrain.transform.position;
            Vector3 splatMapPosition = new Vector3(
                terrainPosition.x / terrain.terrainData.size.x,
                0,
                terrainPosition.z / terrain.terrainData.size.z
            );

            int x = Mathf.FloorToInt(splatMapPosition.x * terrain.terrainData.alphamapWidth);
            int z = Mathf.FloorToInt(splatMapPosition.z * terrain.terrainData.alphamapHeight);

            float[,,] alphaMap = terrain.terrainData.GetAlphamaps(x, z, 1, 1);

            List<TextureAlpha> activeTextures = new List<TextureAlpha>();
            for (int i = 0; i < alphaMap.Length; i++)
            {
                if (alphaMap[0, 0, i] > 0)
                {
                    activeTextures.Add(new TextureAlpha()
                    {
                        Texture = terrain.terrainData.terrainLayers[i].diffuseTexture,
                        Alpha = alphaMap[0, 0, i]
                    });
                }
            }

            return activeTextures;
        }

        private Texture GetActiveTextureFromRenderer(Renderer rend, int triangleIndex)
        {
            if (rend.TryGetComponent(out MeshFilter meshFilter))
            {
                Mesh mesh = meshFilter.mesh;
                if (mesh.subMeshCount > 1)
                {
                    int[] hitTriangleIndices = 
                    {
                        mesh.triangles[triangleIndex * 3],
                        mesh.triangles[triangleIndex * 3 + 1],
                        mesh.triangles[triangleIndex * 3 + 2]
                    };

                    for (int i = 0; i < mesh.subMeshCount; i++)
                    {
                        int[] submeshTriangles = mesh.GetTriangles(i);
                        for (int j = 0; j < submeshTriangles.Length; j += 3)
                        {
                            if (submeshTriangles[j] == hitTriangleIndices[0]
                                && submeshTriangles[j + 1] == hitTriangleIndices[1]
                                && submeshTriangles[j + 2] == hitTriangleIndices[2])
                            {
                                return rend.sharedMaterials[i].mainTexture;
                            }
                        }
                    }
                }
                else
                {
                    return rend.sharedMaterial.mainTexture;
                }
            }
            else if (rend is SpriteRenderer spriteRend)
            {
                return spriteRend.sprite.texture;
            }

            Debug.LogWarning(
                $"{rend.name} has no MeshFilter! Using default impact effect instead of texture-specific one because we'll be unable to find the correct texture!");
            return null;
        }

        private void PlayEffects(Vector3 hitPoint, Vector3 hitNormal, SurfaceEffect surfaceEffect, GameObject hitObject)
        {
            foreach (SpawnObjectEffect spawnObjectEffect in surfaceEffect.spawnObjectEffects)
            {
                if (!spawnObjectEffect.prefab)
                {
                    Debug.LogError("Spawn Object Effect has no prefab!");
                    continue;
                }
                
                if (spawnObjectEffect.probability > Random.value)
                {
                    GameObject spawnObj;
                    if (spawnObjectEffect.isAttachedToImpactSurface && hitObject.activeInHierarchy)
                    {
                        spawnObj = ObjectPoolManager.Instance.SpawnObject(spawnObjectEffect.prefab.gameObject, hitObject.transform);
                        
                        spawnObj.transform.position = hitPoint + hitNormal * .001f;
                        spawnObj.transform.rotation = spawnObjectEffect.canRotate
                            ? Quaternion.LookRotation(hitNormal)
                            : Quaternion.identity;
                    }
                    else
                    {
                        spawnObj = ObjectPoolManager.Instance.SpawnObject(spawnObjectEffect.prefab.gameObject, 
                            hitPoint + hitNormal * .001f, spawnObjectEffect.canRotate 
                            ? Quaternion.LookRotation(hitNormal) : Quaternion.identity
                            , ObjectPoolManager.PoolType.VFX);
                    }
                    
                    if (spawnObjectEffect.randomizeRotation)
                    {
                        Vector3 offset = new Vector3(
                            Random.Range(0, 180 * spawnObjectEffect.randomizedRotationMultiplier.x),
                            Random.Range(0, 180 * spawnObjectEffect.randomizedRotationMultiplier.y),
                            Random.Range(0, 180 * spawnObjectEffect.randomizedRotationMultiplier.z)
                        );

                        spawnObj.transform.rotation = Quaternion.Euler(transform.rotation.eulerAngles + offset);
                    }
                }
            }

            if (surfaceEffect.randomizedOneAudio)
            {
                var randomAudio = surfaceEffect.playAudioEffects[Random.Range(0, surfaceEffect.playAudioEffects.Count)];
                
                soundBuilder
                    .WithPosition(gameObject.transform.position)
                    .WithRandomPitch().Play(randomAudio);
            }
            else
            {
                foreach (var playAudioEffect in surfaceEffect.playAudioEffects)
                {
                    soundBuilder
                        .WithPosition(gameObject.transform.position)
                        .WithRandomPitch().Play(playAudioEffect);
                }
            }
        }
        
        private class TextureAlpha
        {
            public float Alpha;
            public Texture Texture;
        }
    }
}
