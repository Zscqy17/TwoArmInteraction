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

using Oculus.Interaction.Input;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Oculus.Interaction.PoseDetection
{
    /// <summary>
    /// Used in hand pose detection (often as part of a <see cref="Sequence"/> to get the current state of the
    /// hand's Transforms and compare it to the required states. The "Transform states" in question broadly
    /// pertain to the hand's orientation in user-relative terms; see <see cref="TransformFeature"/> for details.
    /// If the current state of the hand transforms meets the specified requirements, <see cref="Active"/> is true.
    /// </summary>
    public class CustomPosesActiveState : MonoBehaviour, IActiveState
    {
        /// <summary>
        /// The hand to read for transform state data.
        /// </summary>
        [SerializeField, Interface(typeof(IHand))]
        [Optional(OptionalAttribute.Flag.Obsolete)]
        private UnityEngine.Object _hand;

        /// <summary>
        /// The <see cref="IHand"/> to be observed. While this hand adopts a pose which meets the requirements,
        /// <see cref="Active"/> will be true.
        /// </summary>
        [Obsolete]
        public IHand Hand { get; private set; }

        /// <summary>
        /// An <cref="ITransformFeatureStateProvider" />, which provides the current state of the tracked hand's transforms.
        /// </summary>
        [SerializeField, Interface(typeof(ITransformFeatureStateProvider))]
        private UnityEngine.Object _transformFeatureStateProvider;

        protected ITransformFeatureStateProvider TransformFeatureStateProvider;
        /// <summary>
        /// The asset containing required transforms that the tracked hand must match for the pose to become active
        /// (assuming all shapes are also active). Each transform is an orientation and a boolean (ex. PalmTowardsFace is True.)
        /// </summary>
        [SerializeField]
        private TransformFeatureConfigAsset _transformFeatureConfigAsset;

        /// <summary>
        /// Influences state transitions computed via <cref="TransformFeatureStateProvider" />. It becomes active whenever all of the listed transform states are active.
        /// State provider uses this to determine the state of features during real time, so edit at runtime at your own risk.
        /// </summary>
        [SerializeField]
        [Tooltip("State provider uses this to determine the state of features during real time, so" +
            " edit at runtime at your own risk.")]
        private TransformConfig _transformConfig;

        /// <summary>
        /// The list of <see cref="TransformFeatureConfig"/>s which must be satisfied for recognition by this TransformRecognizerActiveState.
        /// </summary>
        public IReadOnlyList<TransformFeatureConfig> FeatureConfigs =>
            _transformFeatureConfigAsset != null ? _transformFeatureConfigAsset.Values : Array.Empty<TransformFeatureConfig>();

        /// <summary>
        /// The transform config used in conjunction with an <see cref="ITransformFeatureStateProvider"/> and data from the
        /// <see cref="FeatureConfigs"/> to calculate whether or not this recognizer should be <see cref="Active"/>.
        /// </summary>
        public TransformConfig TransformConfig => _transformConfig;

        protected bool _started = false;

        protected virtual void Awake()
        {
#pragma warning disable CS0612 // Type or member is obsolete
            Hand = _hand as IHand;
#pragma warning restore CS0612 // Type or member is obsolete
            TransformFeatureStateProvider =
                _transformFeatureStateProvider as ITransformFeatureStateProvider;
        }

        protected virtual void Start()
        {
            this.BeginStart(ref _started);
            this.AssertField(TransformFeatureStateProvider, nameof(TransformFeatureStateProvider));

            this.AssertField(_transformFeatureConfigAsset, nameof(_transformFeatureConfigAsset));
            this.AssertField(_transformConfig, nameof(_transformConfig));

            _transformConfig.InstanceId = GetInstanceID();
            this.EndStart(ref _started);
        }

        protected virtual void OnEnable()
        {
            if (_started)
            {
                TransformFeatureStateProvider.RegisterConfig(_transformConfig);

                // Warm up the proactive evaluation
                InitStateProvider();
            }
        }

        protected virtual void OnDisable()
        {
            if (_started)
            {
                TransformFeatureStateProvider.UnRegisterConfig(_transformConfig);
            }
        }

        private void InitStateProvider()
        {
            foreach (var featureConfig in FeatureConfigs)
            {
                TransformFeatureStateProvider.GetCurrentState(_transformConfig, featureConfig.Feature, out _);
            }
        }

        /// <summary>
        /// Retrieves the feature vector for the given inputs, invoking the internal <see cref="ITransformFeatureStateProvider"/>'s
        /// <see cref="ITransformFeatureStateProvider.GetFeatureVectorAndWristPos(TransformConfig, TransformFeature, bool, ref Vector3?, ref Vector3?)"/>
        /// method with the <see cref="TransformConfig"/> and the provided arguments.
        /// </summary>
        /// <remarks>
        /// A "feature vector" in this case is simply a Vector3 whose value should be interpreted in some specific way depending on which
        /// <see cref="TransformFeature"/>. This is an internal API which you should not invoke directly in typical usage.
        /// </remarks>
        /// <param name="feature">The <see cref="TransformFeature"/> for which to retrieve the vector.</param>
        /// <param name="isHandVector">A boolean indicating whether the feature is being requested for a hand or for a controller.</param>
        /// <param name="featureVec">Output parameter to be populated with the requested feature vector.</param>
        /// <param name="wristPos">Output parameter to be populated with the wrist position to which the feature vector is related.</param>
        public void GetFeatureVectorAndWristPos(TransformFeature feature, bool isHandVector,
            ref Vector3? featureVec, ref Vector3? wristPos)
        {
            TransformFeatureStateProvider.GetFeatureVectorAndWristPos(
                TransformConfig, feature, isHandVector, ref featureVec, ref wristPos);
        }

        /// <summary>
        /// Implements <see cref="IActiveState.Active"/>, in this case indicating whether the monitored transform currently
        /// satisfies the recognition conditions specified in the <see cref="TransformConfig"/> and <see cref="FeatureConfigs"/>.
        /// </summary>
        public bool Active
        {
            get
            {
                if (!isActiveAndEnabled)
                {
                    return false;
                }
                foreach (var featureConfig in FeatureConfigs)
                {
                    if (!TransformFeatureStateProvider.IsStateActive(
                        _transformConfig,
                        featureConfig.Feature,
                        featureConfig.Mode,
                        featureConfig.State))
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        #region Inject

        /// <summary>
        /// Sets all required dependencies for a dynamically instantiated TransformRecognizerActiveState. This is a convenience
        /// method which wraps invocations of <see cref="InjectHand(IHand)"/>,
        /// <see cref="InjectTransformFeatureStateProvider(ITransformFeatureStateProvider)"/>,
        /// <see cref="InjectTransformFeatureAsset(TransformFeatureConfigAsset)"/> and <see cref="InjectTransformConfig(TransformConfig)"/>.
        /// This method exists to support Interaction SDK's dependency injection pattern and is not needed for typical Unity Editor-based
        /// usage.
        /// </summary>
        [Obsolete("Use InjectAllTransformRecognizerActiveState(ITransformFeatureStateProvider, TransformFeatureConfigAsset, TransformConfig) instead")]
        public void InjectAllTransformRecognizerActiveState(IHand hand,
            ITransformFeatureStateProvider transformFeatureStateProvider,
            TransformFeatureConfigAsset transformFeatureAsset,
            TransformConfig transformConfig)
        {
            InjectHand(hand);
            InjectTransformFeatureStateProvider(transformFeatureStateProvider);
            InjectTransformFeatureAsset(transformFeatureAsset);
            InjectTransformConfig(transformConfig);
        }

        /// <summary>
        /// Sets all required dependencies for a dynamically instantiated TransformRecognizerActiveState. This is a convenience
        /// method which wraps invocations of <see cref="InjectHand(IHand)"/>,
        /// <see cref="InjectTransformFeatureStateProvider(ITransformFeatureStateProvider)"/>,
        /// <see cref="InjectTransformFeatureAsset(TransformFeatureConfigAsset)"/> and <see cref="InjectTransformConfig(TransformConfig)"/>.
        /// This method exists to support Interaction SDK's dependency injection pattern and is not needed for typical Unity Editor-based
        /// usage.
        /// </summary>
        public void InjectAllTransformRecognizerActiveState(
            ITransformFeatureStateProvider transformFeatureStateProvider,
            TransformFeatureConfigAsset transformFeatureAsset,
            TransformConfig transformConfig)
        {
            InjectTransformFeatureStateProvider(transformFeatureStateProvider);
            InjectTransformFeatureAsset(transformFeatureAsset);
            InjectTransformConfig(transformConfig);
        }

        /// <summary>
        /// Sets an <see cref="IHand"/> as the <see cref="Hand"/> for a dynamically instantiated
        /// TransformRecognizerActiveState. This method exists to support Interaction SDK's dependency injection pattern and is not needed for
        /// typical Unity Editor-based usage.
        /// </summary>
        [Obsolete]
        public void InjectHand(IHand hand)
        {
            _hand = hand as UnityEngine.Object;
            Hand = hand;
        }

        /// <summary>
        /// Sets an <see cref="ITransformFeatureStateProvider"/> as the feature state provider (which assesses whether or not the monitored
        /// hand's transforms meet the requirements for recognition) for a dynamically instantiated
        /// TransformRecognizerActiveState. This method exists to support Interaction SDK's dependency injection pattern and is not needed for
        /// typical Unity Editor-based usage.
        /// </summary>
        public void InjectTransformFeatureStateProvider(ITransformFeatureStateProvider transformFeatureStateProvider)
        {
            TransformFeatureStateProvider = transformFeatureStateProvider;
            _transformFeatureStateProvider = transformFeatureStateProvider as UnityEngine.Object;
        }

        /// <summary>
        /// Sets a <see cref="TransformFeatureConfigAsset"/> as the <see cref="FeatureConfigs"/> for a dynamically instantiated
        /// TransformRecognizerActiveState. This method exists to support Interaction SDK's dependency injection pattern and is not needed for
        /// typical Unity Editor-based usage.
        /// </summary>
        public void InjectTransformFeatureAsset(TransformFeatureConfigAsset transformFeatureAsset)
        {
            _transformFeatureConfigAsset = transformFeatureAsset;
        }

        /// <summary>
        /// Sets a <see cref="PoseDetection.TransformConfig"/> as the <see cref="TransformConfig"/> for a dynamically instantiated
        /// TransformRecognizerActiveState. This method exists to support Interaction SDK's dependency injection pattern and is not needed for
        /// typical Unity Editor-based usage.
        /// </summary>
        public void InjectTransformConfig(TransformConfig transformConfig)
        {
            _transformConfig = transformConfig;
        }
        #endregion
    }
}