using UnityEditor.Graphing;
using UnityEditor.ShaderGraph.Drawing.Controls;
using UnityEngine;

namespace UnityEditor.ShaderGraph
{
    [Title("Math/Vector/Transform")]
    public class TransformNode : AbstractMaterialNode, IGeneratesBodyCode, IMayRequireTangent, IMayRequireBitangent, IMayRequireNormal
    {
        [SerializeField]
        private SimpleMatrixType m_spaceListFrom;
        [SerializeField]
        private SimpleMatrixType m_spaceListTo;

        private const int InputSlotId = 0;
        private const int OutputSlotId = 1;
        private const string kInputSlotName = "Input";
        private const string kOutputSlotName = "Output";

        [EnumControl("From")]
        public SimpleMatrixType spaceFrom
        {
            get { return m_spaceListFrom; }
            set
            {
                if (m_spaceListFrom == value)
                    return;

                m_spaceListFrom = value;
                if (onModified != null)
                {
                    onModified(this, ModificationScope.Graph);
                }
            }
        }

        public override bool hasPreview
        {
            get { return false; }
        }

        [EnumControl("To")]
        public SimpleMatrixType spaceTo
        {
            get { return m_spaceListTo; }
            set
            {
                if (m_spaceListTo == value)
                    return;

                m_spaceListTo = value;
                if (onModified != null)
                {
                    onModified(this, ModificationScope.Graph);
                }
            }
        }

        public TransformNode()
        {
            name = "Transform";
            UpdateNodeAfterDeserialization();
        }

        public sealed override void UpdateNodeAfterDeserialization()
        {
            AddSlot(GetInputSlot());
            AddSlot(GetOutputSlot());
            RemoveSlotsNameNotMatching(validSlots);
        }

        protected int[] validSlots
        {
            get { return new[] {InputSlotId, OutputSlotId}; }
        }

        protected virtual MaterialSlot GetInputSlot()
        {
            return new Vector3MaterialSlot(InputSlotId, GetInputSlotName(), kInputSlotName, SlotType.Input, Vector3.zero);
        }

        protected virtual MaterialSlot GetOutputSlot()
        {
            return new Vector3MaterialSlot(OutputSlotId, GetOutputSlotName(), kOutputSlotName, SlotType.Output, Vector4.zero);
        }

        protected virtual string GetInputSlotName()
        {
            return "Input";
        }

        protected virtual string GetOutputSlotName()
        {
            return "Output";
        }

        public void GenerateNodeCode(ShaderGenerator visitor, GenerationMode generationMode)
        {
            NodeUtils.SlotConfigurationExceptionIfBadConfiguration(this, new[] { InputSlotId }, new[] { OutputSlotId });
            string inputValue = GetSlotValue(InputSlotId, generationMode);
            string transformString = "";
            bool requiresTangentTransform = false;

            if (spaceFrom == SimpleMatrixType.World)
            {
                if (spaceTo == SimpleMatrixType.World)
                {
                    transformString = inputValue;
                }
                else if (spaceTo == SimpleMatrixType.Local)
                {
                    transformString = "mul(unity_WorldToObject, float4(" + inputValue + ", 0)).xyz";
                }
                else if (spaceTo == SimpleMatrixType.Tangent)
                {
                    requiresTangentTransform = true;
                    transformString = "mul(tangentTransform, " + inputValue + ").xyz";
                }
                else if (spaceTo == SimpleMatrixType.View)
                {
                    transformString = "mul( UNITY_MATRIX_V, float4(" + inputValue + ", 0)).xyz";
                }
            }
            else if (spaceFrom == SimpleMatrixType.Local)
            {
                if (spaceTo == SimpleMatrixType.World)
                {
                    transformString = "mul(unity_ObjectToWorld, float4(" + inputValue + ", 0)).xyz";
                }
                else if (spaceTo == SimpleMatrixType.Local)
                {
                    transformString = inputValue;
                }
                else if (spaceTo == SimpleMatrixType.Tangent)
                {
                    requiresTangentTransform = true;
                    transformString = "mul( tangentTransform, mul( unity_ObjectToWorld, float4(" + inputValue + ", 0) ).xyz).xyz";
                }
                else if (spaceTo == SimpleMatrixType.View)
                {
                    transformString = "mul( UNITY_MATRIX_MV, float4(" + inputValue + ", 0)).xyz";
                }
            }
            else if (spaceFrom == SimpleMatrixType.Tangent)
            {
                requiresTangentTransform = true;
                if (spaceTo == SimpleMatrixType.World)
                {
                    transformString = "mul( " + inputValue + ", tangentTransform ).xyz";
                }
                else if (spaceTo == SimpleMatrixType.Local)
                {
                    transformString = "mul( unity_WorldToObject, float4(mul(" + inputValue + ", tangentTransform ),0) ).xyz";
                }
                else if (spaceTo == SimpleMatrixType.Tangent)
                {
                    transformString = inputValue;
                }
                else if (spaceTo == SimpleMatrixType.View)
                {
                    transformString = "mul( UNITY_MATRIX_V, float4(mul(" + inputValue + ", tangentTransform ),0) ).xyz";
                }
            }
            else if (spaceFrom == SimpleMatrixType.View)
            {
                if (spaceTo == SimpleMatrixType.World)
                {
                    transformString = "mul( float4(" + inputValue + ", 0), UNITY_MATRIX_V ).xyz";
                }
                else if (spaceTo == SimpleMatrixType.Local)
                {
                    transformString = "mul( float4(" + inputValue + ", 0), UNITY_MATRIX_MV ).xyz";
                }
                else if (spaceTo == SimpleMatrixType.Tangent)
                {
                    requiresTangentTransform = true;
                    transformString = "mul( tangentTransform, mul( float4(" + inputValue + ", 0), UNITY_MATRIX_V ).xyz ).xyz";
                }
                else if (spaceTo == SimpleMatrixType.View)
                {
                    transformString = inputValue;
                }
            }

            if (requiresTangentTransform)
                visitor.AddShaderChunk("float3x3 tangentTransform = float3x3( worldSpaceTangent, worldSpaceBitangent, worldSpaceNormal);", false);

            visitor.AddShaderChunk(string.Format("{0} {1} = {2};",
                ConvertConcreteSlotValueTypeToString(precision, FindOutputSlot<MaterialSlot>(OutputSlotId).concreteValueType), 
                GetVariableNameForSlot(OutputSlotId),
                transformString), true);
        }

        //float3x3 tangentTransform = float3x3( i.tangentDir, i.bitangentDir, i.normalDir);------

        //mul(unity_WorldToObject, float4(i.posWorld.rgb,0) ).xyz - world to local---------
        //mul( tangentTransform, i.posWorld.rgb ).xyz - world to tangent-----------------
        //mul( UNITY_MATRIX_V, float4(i.posWorld.rgb,0) ).xyz - world to view-------------

        //mul( unity_ObjectToWorld, float4(i.posWorld.rgb,0) ).xyz - local to world--------
        //mul( tangentTransform, mul( unity_ObjectToWorld, float4(i.posWorld.rgb,0) ).xyz - local to tangent------------
        //mul( UNITY_MATRIX_MV, float4(i.posWorld.rgb,0) ).xyz - local to view--------------

        //mul( i.posWorld.rgb, tangentTransform ).xyz - tangent to world---------
        //mul( unity_WorldToObject, float4(mul( i.posWorld.rgb, tangentTransform ),0) ).xyz - tangent to local-----
        //mul( UNITY_MATRIX_V, float4(mul( i.posWorld.rgb, tangentTransform ),0) ).xyz - tangent to view-------

        //mul( float4(i.posWorld.rgb,0), UNITY_MATRIX_V ).xyz - view to world
        //mul( float4(i.posWorld.rgb,0), UNITY_MATRIX_MV ).xyz - view to local
        //mul( tangentTransform, mul( float4(i.posWorld.rgb,0), UNITY_MATRIX_V ).xyz ).xyz - view to tangent

        public NeededCoordinateSpace RequiresTangent()
        {
            return NeededCoordinateSpace.World;
        }

        public NeededCoordinateSpace RequiresBitangent()
        {
            return NeededCoordinateSpace.World;
        }

        public NeededCoordinateSpace RequiresNormal()
        {
            return NeededCoordinateSpace.World;
        }
    }
}