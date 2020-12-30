using System;
using System.Linq;
using System.Threading.Tasks;
using ThunderRoad;
using UnityEngine;
using UnityEngine.AddressableAssets;
using IngameDebugConsole;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace ThunderRoad {
    using UnityEngine.AddressableAssets.ResourceLocators;
    class ConsoleCommands : MonoBehaviour {
        [ConsoleMethod("addressables", "list all loaded addressable names")]
        public static string GetAddressables() {
            ResourceLocationMap map = Addressables.ResourceLocators as ResourceLocationMap;
            return string.Join(", ", map.Keys.ToList());
        }
    }
}

namespace Utils {
    namespace ExtensionMethods {
        static class TaskExtensions {
            public static Task<TOutput> Then<TInput, TOutput>(this Task<TInput> task, Func<TInput, TOutput> func) {
                return task.ContinueWith((input) => func(input.Result));
            }
            public static Task Then(this Task task, Action<Task> func) {
                return task.ContinueWith(func);
            }
            public static Task Then<TInput>(this Task<TInput> task, Action<TInput> func) {
                return task.ContinueWith((input) => func(input.Result));
            }
            public static T GetOrAddComponent<T>(this GameObject obj) where T : Component {
                return obj.GetComponent<T>() ?? obj.AddComponent<T>();
            }
        }
    }
    class Utils {
        public static Item SpawnItemSync(string itemId) {
            return SpawnItemSync(Catalog.GetData<ItemPhysic>(itemId));
        }
        public static Item SpawnItemSync(ItemPhysic itemData) {
            return SpawnItem(itemData).GetAwaiter().GetResult();
        }
        public static async Task<Item> SpawnItem(string itemId) {
            return await SpawnItem(Catalog.GetData<ItemPhysic>(itemId));
        }
        public static Task<Item> SpawnItem(ItemPhysic itemData) {
            var promise = new TaskCompletionSource<Item>();
            System.Action action = () => itemData.SpawnAsync(item => promise.SetResult(item));
            Task.Run(action);
            return promise.Task;
        }
        public static void RefreshEffectPools() {
            //EffectModuleAudio.pool = EffectModuleAudio.pool.Where(t => t != null).ToList();
            //EffectModuleAudio.GeneratePool();
            Debug.Log("Re-generating audio pool...");
            int nullCount = 0;
            for (int index = 0; index < EffectModuleAudio.pool.Count(); index++) {
                if (EffectModuleAudio.pool[index] == null) {
                    nullCount++;
                    GameObject gameObject = new GameObject("Audio" + (object)index);
                    gameObject.transform.SetParent(EffectModuleAudio.poolRoot);
                    EffectAudio effectAudio = gameObject.AddComponent<EffectAudio>();
                    effectAudio.isPooled = true;
                    gameObject.SetActive(false);
                    EffectModuleAudio.pool[index] = effectAudio;
                }
            }
            Debug.Log($"Found and re-generated {nullCount} audio pool entries.");
            //EffectModuleVfx.pool = EffectModuleVfx.pool.Where(t => t != null).ToList();
            //EffectModuleVfx.GeneratePool();
            //EffectModuleDecal.pool = EffectModuleDecal.pool.Where(t => t != null).ToList();
            //EffectModuleDecal.GeneratePool();
            //EffectModulePaint.pool = EffectModulePaint.pool.Where(t => t != null).ToList();
            //EffectModulePaint.GeneratePool();
            //EffectModuleParticle.pool = EffectModuleParticle.pool.Where(t => t.Key != null && t.Value != null).ToDictionary(t => t.Key, t => t.Value);
            //EffectModuleParticle.GeneratePool();
            //EffectModuleShader.pool = EffectModuleShader.pool.Where(t => t != null).ToList();
            //EffectModuleShader.GeneratePool();
            //EffectModuleMesh.pool = EffectModuleMesh.pool.Where(t => t != null).ToList();
            //EffectModuleMesh.GeneratePool();
        }
    }
}

