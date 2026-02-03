/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * Licensed under the Oculus SDK License Agreement (the "License");
 * you may not use the Oculus SDK except in compliance with the License,
 * which is provided at the time of installation or download, or which
 * otherwise accompanies this software in either electronic or hard copy form.
 *
 * You may obtain a copy of the License at
 *
 * https://developer.oculus.com/licenses/oculussdk/
 *
 * Unless required by applicable law or agreed to in writing, the Oculus SDK
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System.Collections.Generic;
using UnityEngine;

namespace Oculus.Interaction.PoseDetection
{
    /// <summary>
    /// ScriptableObject asset that stores a list of <see cref="TransformFeatureConfig"/>s for inspector assignment.
    /// </summary>
    /// <remarks>
    /// Each TransformFeatureConfig describes only one aspect (<see cref="TransformFeature"/>) of a transform,
    /// meaning multiple can be applicable at the same time; for example, the "stop" hand gesture must satisfy
    /// both <see cref="TransformFeature.FingersUp"/> and <see cref="TransformFeature.PalmAwayFromFace"/>. The
    /// list can thus be thought of as a set of requirements, all of which must be satisfied for recognition.
    /// </remarks>
    [CreateAssetMenu(menuName = "Meta/Interaction/SDK/Pose Detection/Transform Feature Configs")]
    public class TransformFeatureConfigAsset : ScriptableObject
    {
        [SerializeField]
        private List<TransformFeatureConfig> _values = new();

        /// <summary>
        /// The list of <see cref="TransformFeatureConfig"/>s which must all be satisfied in order for the
        /// associated transform to be considered acceptable for recognition.
        /// </summary>
        public IReadOnlyList<TransformFeatureConfig> Values => _values;

        /// <summary>
        /// Factory for TransformFeatureConfigAssets, allowing them to be constructed from runtime-generated
        /// configurations.
        /// </summary>
        /// <param name="values">The <see cref="TransformFeatureConfig"/>s constituent to this list</param>
        public static TransformFeatureConfigAsset Create(List<TransformFeatureConfig> values)
        {
            TransformFeatureConfigAsset asset = CreateInstance<TransformFeatureConfigAsset>();
            asset._values = values;
            return asset;
        }
    }
}
