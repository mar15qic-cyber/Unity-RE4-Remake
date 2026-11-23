#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

namespace RE4.ChapterOne
{
    [PreferBinarySerialization]
    public sealed class ChapterOneBakedClips : ScriptableObject
    {
        [SerializeField] private AnimationClip[] sourceClips;
        [SerializeField] private AnimationClip[] bakedClips;

        internal Dictionary<AnimationClip, AnimationClip> ReadSourceLinks()
        {
            var links = new Dictionary<AnimationClip, AnimationClip>();
            if (sourceClips != null && bakedClips != null)
            {
                for (int index = 0; index < sourceClips.Length; index++)
                {
                    if (sourceClips[index] != null && bakedClips[index] != null)
                    {
                        links.Add(sourceClips[index], bakedClips[index]);
                    }
                }
            }
            return links;
        }

        internal void WriteSourceLinks(Dictionary<AnimationClip, AnimationClip> links)
        {
            sourceClips = new AnimationClip[links.Count];
            bakedClips = new AnimationClip[links.Count];
            links.Keys.CopyTo(sourceClips, 0);
            links.Values.CopyTo(bakedClips, 0);
        }
    }
}
#endif

