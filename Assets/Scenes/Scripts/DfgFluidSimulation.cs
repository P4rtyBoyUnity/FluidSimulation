using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.DataFlowGraph;
using Unity.Burst;

public class AdvectionNode : NodeDefinition<AdvectionNode.InstanceData, AdvectionNode.KernelData, AdvectionNode.Ports, AdvectionNode.GraphKernel>
{
    public struct KernelData : IKernelData {}

    public struct InstanceData : INodeData {}

    public struct Ports : IKernelPortDefinition
    {
        // Common Inputs
        public DataInput<AdvectionNode, float> inDeltaT;
        public DataInput<AdvectionNode, float> inVolumeToAdd;

        // Node specific inputs
        public DataInput<AdvectionNode, float> inHeight;
        public DataInput<AdvectionNode, float> inSpeed;

        // Outputs
        public DataOutput<AdvectionNode, float> outSpeed;
        public DataOutput<AdvectionNode, float> outHeight;
    }

    public struct GraphKernel : IGraphKernel<KernelData, Ports>
    {
        //float height;

        public void Execute(RenderContext ctx, KernelData data, ref Ports ports)
        {
            // Read inputs
            float speed = ctx.Resolve(ports.inSpeed);
            float height = ctx.Resolve(ports.inHeight);
            float volumeToAdd = ctx.Resolve(ports.inVolumeToAdd);
            float deltaT = ctx.Resolve(ports.inDeltaT);

            // Compute
            height = Mathf.Max(height + volumeToAdd + speed * deltaT, 0.0f);

            // Write outputs
            ref var outputSpeed = ref ctx.Resolve(ref ports.outSpeed);
            outputSpeed = speed;
            ref var outputHeight = ref ctx.Resolve(ref ports.outHeight);
            outputHeight = height;
        }
    }
}

public class DiffusionNode : NodeDefinition<DiffusionNode.InstanceData, DiffusionNode.KernelData, DiffusionNode.Ports, DiffusionNode.GraphKernel>
{ 
    public struct KernelData : IKernelData { }

    public struct InstanceData : INodeData { }

    public struct Ports : IKernelPortDefinition
    {
        // Common Inputs
        public DataInput<DiffusionNode, float> inDeltaT;
        public DataInput<DiffusionNode, float> inDiffusionSpeed;
        public DataInput<DiffusionNode, float> inViscosity;

        // Node specific inputs
        public DataInput<DiffusionNode, float> inSpeed;
        public DataInput<DiffusionNode, float> inHeight;
        public DataInput<DiffusionNode, float> inHeightPrevX, inHeightPrevZ, inHeightNextX, inHeightNextZ;

        // Outputs
        public DataOutput<DiffusionNode, float> outSpeed;
        public DataOutput<DiffusionNode, float> outHeight;
    }

    public struct GraphKernel : IGraphKernel<KernelData, Ports>
    {
        //float speed;

        public void Execute(RenderContext ctx, KernelData data, ref Ports ports)
        {
            // Read inputs
            float transferRate = ctx.Resolve(ports.inDiffusionSpeed) * ctx.Resolve(ports.inDeltaT);
            float viscosity = ctx.Resolve(ports.inViscosity);
            float height = ctx.Resolve(ports.inHeight);
            float speed = ctx.Resolve(ports.inSpeed);
            float prevX = ctx.Resolve(ports.inHeightPrevX);
            float nextX = ctx.Resolve(ports.inHeightNextX);
            float prevZ = ctx.Resolve(ports.inHeightPrevZ);
            float nextZ = ctx.Resolve(ports.inHeightNextZ);            

            // Compute
            speed += ((prevX + nextX + prevZ + nextZ) / 4.0f - height) * transferRate;
            speed  *= viscosity;            

            // Write outputs
            ref var outputSpeed = ref ctx.Resolve(ref ports.outSpeed);
            outputSpeed = speed;
            ref var outputHeight = ref ctx.Resolve(ref ports.outHeight);
            outputHeight = height;
        }
    }
}

public class UniformNode: NodeDefinition<UniformNode.InstanceData, UniformNode.KernelData, UniformNode.Ports, UniformNode.GraphKernel>
{
    public struct KernelData : IKernelData { }

    public struct InstanceData : INodeData { }

    public struct Ports : IKernelPortDefinition
    {
        // Inputs
        public DataInput<UniformNode, float> inDeltaT;
        public DataInput<UniformNode, float> inDiffusionSpeed;
        public DataInput<UniformNode, float> inViscosity;
        public DataInput<UniformNode, float> inVolumeToAdd;

        // Outputs
        public DataOutput<UniformNode, float> outDeltaT;
        public DataOutput<UniformNode, float> outDiffusionSpeed;
        public DataOutput<UniformNode, float> outViscosity;
        public DataOutput<UniformNode, float> outVolumeToAdd;
    }

    public struct GraphKernel : IGraphKernel<KernelData, Ports>
    {
        public void Execute(RenderContext ctx, KernelData data, ref Ports ports)
        {
            ref var deltaT = ref ctx.Resolve(ref ports.outDeltaT);
            deltaT = ctx.Resolve(ports.inDeltaT);

            ref var diffusionSpeed = ref ctx.Resolve(ref ports.outDiffusionSpeed);
            diffusionSpeed = ctx.Resolve(ports.inDiffusionSpeed);

            ref var viscosity = ref ctx.Resolve(ref ports.outViscosity);
            viscosity = ctx.Resolve(ports.inViscosity);            

            ref var volumeToAdd = ref ctx.Resolve(ref ports.outVolumeToAdd);
            volumeToAdd = ctx.Resolve(ports.inVolumeToAdd);
        }
    }
}

[System.Serializable]
public class DfgSimulation : FluidSimulationInterface
{
    private Vector3[] vertices = null;
    private float[] speed = null;
    //List<NodeHandle<ScatteringNode>> m_ScatteringNodeList = new List<NodeHandle<ScatteringNode>>();
    NodeHandle<UniformNode> m_UniformNode;
    List<NodeHandle<DiffusionNode>> m_DiffusionNodeList = new List<NodeHandle<DiffusionNode>>();
    List<NodeHandle<AdvectionNode>> m_AdvectionNodeList = new List<NodeHandle<AdvectionNode>>();
    List<GraphValue<float>>         m_SpeedGraphValues = new List<GraphValue<float>>();
    List<GraphValue<float>>         m_HeightGraphValues = new List<GraphValue<float>>();
    NodeSet m_Set;

    public DfgSimulation(Neighbor[] neighbor, ref Vector3[] vertices, float diffusionSpeed, float viscosity)
    {
        this.vertices = vertices;
        m_Set = new NodeSet();
        m_Set.RendererModel = NodeSet.RenderExecutionModel.Islands;
        speed = new float[vertices.Length];

        /*
        // Create Scattering nodes
        for (uint i = 0; i < vertices.Length; i++)
            m_ScatteringNodeList.Add(m_Set.Create<ScatteringNode>());
        */

        // Create uniform node (the one with all shared values)
        m_UniformNode = m_Set.Create<UniformNode>();

        for (uint i = 0; i < vertices.Length; i++)
        {
            // Create Diffusions nodes
            m_DiffusionNodeList.Add(m_Set.Create<DiffusionNode>());
            // Create Advections nodes
            m_AdvectionNodeList.Add(m_Set.Create<AdvectionNode>());
            // Create Speed reading graph values
            m_SpeedGraphValues.Add(m_Set.CreateGraphValue(m_DiffusionNodeList[(int)i], DiffusionNode.KernelPorts.outHeight));
            // Create Height reading graph values
            m_HeightGraphValues.Add(m_Set.CreateGraphValue(m_DiffusionNodeList[(int)i], DiffusionNode.KernelPorts.outSpeed));
        }
            

        // Connects Nodes;1 layer = advection, 2nd layer = diffusion        
        for (int i = 0; i < vertices.Length; i++)
        {
            // Connect uniforms to 1st stage (advection)
            m_Set.Connect(m_UniformNode, UniformNode.KernelPorts.outDeltaT, m_AdvectionNodeList[i], AdvectionNode.KernelPorts.inDeltaT);
            m_Set.Connect(m_UniformNode, UniformNode.KernelPorts.outVolumeToAdd, m_AdvectionNodeList[i], AdvectionNode.KernelPorts.inVolumeToAdd);

            // Connect 1st stage advection to 2nd stage diffusion 
            m_Set.Connect(m_AdvectionNodeList[i], AdvectionNode.KernelPorts.outHeight, m_DiffusionNodeList[i], DiffusionNode.KernelPorts.inHeight);
            m_Set.Connect(m_AdvectionNodeList[neighbor[i].prevX], AdvectionNode.KernelPorts.outHeight, m_DiffusionNodeList[i], DiffusionNode.KernelPorts.inHeightPrevX);
            m_Set.Connect(m_AdvectionNodeList[neighbor[i].nextX], AdvectionNode.KernelPorts.outHeight, m_DiffusionNodeList[i], DiffusionNode.KernelPorts.inHeightNextX);
            m_Set.Connect(m_AdvectionNodeList[neighbor[i].prevZ], AdvectionNode.KernelPorts.outHeight, m_DiffusionNodeList[i], DiffusionNode.KernelPorts.inHeightPrevZ);
            m_Set.Connect(m_AdvectionNodeList[neighbor[i].nextZ], AdvectionNode.KernelPorts.outHeight, m_DiffusionNodeList[i], DiffusionNode.KernelPorts.inHeightNextZ);
            m_Set.Connect(m_AdvectionNodeList[i], AdvectionNode.KernelPorts.outSpeed, m_DiffusionNodeList[i], DiffusionNode.KernelPorts.inSpeed);

            // Connect uniforms to 2nd stage (diffusion)
            m_Set.Connect(m_UniformNode, UniformNode.KernelPorts.outDeltaT, m_DiffusionNodeList[i], DiffusionNode.KernelPorts.inDeltaT);
            m_Set.Connect(m_UniformNode, UniformNode.KernelPorts.outDiffusionSpeed, m_DiffusionNodeList[i], DiffusionNode.KernelPorts.inDiffusionSpeed);
            m_Set.Connect(m_UniformNode, UniformNode.KernelPorts.outViscosity, m_DiffusionNodeList[i], DiffusionNode.KernelPorts.inViscosity);
        }

    }

    public void Dispose()
    {
        for (int i = 0; i < vertices.Length; i++)
        {
            m_Set.Destroy(m_AdvectionNodeList[i]);
            m_Set.Destroy(m_DiffusionNodeList[i]);
            m_Set.ReleaseGraphValue(m_SpeedGraphValues[i]);
            m_Set.ReleaseGraphValue(m_HeightGraphValues[i]);
        }    

        m_Set.Destroy(m_UniformNode);
    }

    public void Advection(float volumeToAddPerCell)
    {
        float deltaT = Time.deltaTime;

        // Set uniforms
        m_Set.SetData(m_UniformNode, UniformNode.KernelPorts.inDeltaT, deltaT);
        m_Set.SetData(m_UniformNode, UniformNode.KernelPorts.inVolumeToAdd, volumeToAddPerCell);

        // TODO: Set viscosity/diff speed
        m_Set.SetData(m_UniformNode, UniformNode.KernelPorts.inViscosity, 0.998f);
        m_Set.SetData(m_UniformNode, UniformNode.KernelPorts.inDiffusionSpeed, 20.0f);

        for (int i = 0; i < vertices.Length; i++)
        {
            m_Set.SetData(m_AdvectionNodeList[i], AdvectionNode.KernelPorts.inSpeed, speed[i]);
            m_Set.SetData(m_AdvectionNodeList[i], AdvectionNode.KernelPorts.inHeight, vertices[i].y);            
        }

        m_Set.Update();
    }

    public void Diffusion()
    {
        // Nothing to do, done in ECS
    }

    public void ApplyToMesh()
    {
        for (int i = 0; i < vertices.Length; i++)
            vertices[i].y = m_Set.GetValueBlocking(m_SpeedGraphValues[i]);

        for (int i = 0; i < vertices.Length; i++)
            speed[i] = m_Set.GetValueBlocking(m_HeightGraphValues[i]);
    }

    public float GetSpeed(int index)
    {
        return speed[index];
    }

    public void SetSpeed(int index, float speed)
    {
        this.speed[index] = speed;
    }
}

