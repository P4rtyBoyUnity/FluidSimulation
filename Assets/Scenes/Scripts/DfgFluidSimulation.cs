using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.DataFlowGraph;
using Unity.Burst;


public class DiffusionNode : NodeDefinition<DiffusionNode.InstanceData, DiffusionNode.SimPorts, DiffusionNode.KernelData, DiffusionNode.Ports, DiffusionNode.GraphKernel>, IMsgHandler<float>
{
    public struct KernelData : IKernelData
    {
        public float acceleration;
    }

    public struct InstanceData : INodeData
    {
        public float accumulatedAcceleration;
    }

    public struct Ports : IKernelPortDefinition
    {
        // Common Inputs
        public DataInput<DiffusionNode, float> inDeltaT;
        public DataInput<DiffusionNode, float> inDiffusionSpeed;
        public DataInput<DiffusionNode, float> inViscosity;

        // Node specific inputs
        //public DataInput<DiffusionNode, float> inForce;
        public DataInput<DiffusionNode, float> inHeight;
        public DataInput<DiffusionNode, float> inHeightPrevX, inHeightPrevZ, inHeightNextX, inHeightNextZ;

        // Outputs
        public DataOutput<DiffusionNode, float> outSpeed;
    }

    public struct SimPorts : ISimulationPortDefinition
    {
        public MessageInput<DiffusionNode, float> accelerationInput;
    }

    protected override void OnUpdate(in UpdateContext ctx)
    {
        ref var nodeData = ref GetNodeData(ctx.Handle);
        ref KernelData kernelData = ref GetKernelData(ctx.Handle);
        kernelData.acceleration = nodeData.accumulatedAcceleration;
        nodeData.accumulatedAcceleration = 0.0f;
    }

    public void HandleMessage(in MessageContext ctx, in float msg)
    {
        ref var nodeData = ref GetNodeData(ctx.Handle);
        nodeData.accumulatedAcceleration += msg;
    }

    public struct GraphKernel : IGraphKernel<KernelData, Ports>
    {
        float speed;

        [BurstCompile]
        public void Execute(RenderContext ctx, KernelData data, ref Ports ports)
        {
            // Read inputs
            float deltaT = ctx.Resolve(ports.inDeltaT);
            float diffusionSpeed = ctx.Resolve(ports.inDiffusionSpeed);
            float viscosity = ctx.Resolve(ports.inViscosity);
            float height = ctx.Resolve(ports.inHeight);
            float prevX = ctx.Resolve(ports.inHeightPrevX);
            float nextX = ctx.Resolve(ports.inHeightNextX);
            float prevZ = ctx.Resolve(ports.inHeightPrevZ);
            float nextZ = ctx.Resolve(ports.inHeightNextZ);            

            // Compute
            speed += (data.acceleration + ((prevX + nextX + prevZ + nextZ) / 4.0f - height) * diffusionSpeed) * deltaT;
            speed  *= viscosity;            

            // Write outputs
            ref var outputSpeed = ref ctx.Resolve(ref ports.outSpeed);
            outputSpeed = speed;
        }
    }
}

public class AdvectionNode : NodeDefinition<AdvectionNode.InstanceData, AdvectionNode.KernelData, AdvectionNode.Ports, AdvectionNode.GraphKernel>
{
    public struct KernelData : IKernelData { }

    public struct InstanceData : INodeData { }

    public struct Ports : IKernelPortDefinition
    {
        // Common Inputs
        public DataInput<AdvectionNode, float> inDeltaT;
        public DataInput<AdvectionNode, float> inVolumeToAdd;

        // Node specific inputs
        public DataInput<AdvectionNode, float> inSpeed;

        // Outputs
        public DataOutput<AdvectionNode, float> outHeight;
    }

    public struct GraphKernel : IGraphKernel<KernelData, Ports>
    {
        float height;

        [BurstCompile]
        public void Execute(RenderContext ctx, KernelData data, ref Ports ports)
        {
            // Read inputs
            float speed = ctx.Resolve(ports.inSpeed);
            float volumeToAdd = ctx.Resolve(ports.inVolumeToAdd);
            float deltaT = ctx.Resolve(ports.inDeltaT);

            // Compute
            height = Mathf.Max(height + volumeToAdd + speed * deltaT, 0.0f);

            // Write outputs
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
        [BurstCompile]
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
    NodeHandle<UniformNode> m_UniformNode;
    List<NodeHandle<DiffusionNode>> m_DiffusionNodeList = new List<NodeHandle<DiffusionNode>>();
    List<NodeHandle<AdvectionNode>> m_AdvectionNodeList = new List<NodeHandle<AdvectionNode>>();
    List<GraphValue<float>>         m_HeightGraphValues = new List<GraphValue<float>>();
    NodeSet m_Set;

    public DfgSimulation(Neighbor[] neighbor, ref Vector3[] vertices, float diffusionSpeed, float viscosity)
    {
        this.vertices = vertices;
        m_Set = new NodeSet();
        //m_Set.RendererModel = NodeSet.RenderExecutionModel.Islands;
        m_Set.RendererModel = NodeSet.RenderExecutionModel.SingleThreaded;

        // Create uniform node (the one with all shared values)
        m_UniformNode = m_Set.Create<UniformNode>();

        for (uint i = 0; i < vertices.Length; i++)
        {
            // Create Diffusions nodes
            m_DiffusionNodeList.Add(m_Set.Create<DiffusionNode>());
            // Create Advections nodes
            m_AdvectionNodeList.Add(m_Set.Create<AdvectionNode>());
            // Create Height reading graph values
            m_HeightGraphValues.Add(m_Set.CreateGraphValue(m_AdvectionNodeList[(int)i], AdvectionNode.KernelPorts.outHeight));
        }            

        // Connects Nodes;1 layer = diffusion, 2nd layer = advection
        for (int i = 0; i < vertices.Length; i++)
        {
            // Connect uniforms to 1st stage (diffusion)
            m_Set.Connect(m_UniformNode, UniformNode.KernelPorts.outDeltaT, m_DiffusionNodeList[i], DiffusionNode.KernelPorts.inDeltaT);
            m_Set.Connect(m_UniformNode, UniformNode.KernelPorts.outDiffusionSpeed, m_DiffusionNodeList[i], DiffusionNode.KernelPorts.inDiffusionSpeed);
            m_Set.Connect(m_UniformNode, UniformNode.KernelPorts.outViscosity, m_DiffusionNodeList[i], DiffusionNode.KernelPorts.inViscosity);

            // Connect advection output to 1st stage diffusion inputs
            m_Set.Connect(m_AdvectionNodeList[i], AdvectionNode.KernelPorts.outHeight, m_DiffusionNodeList[i], DiffusionNode.KernelPorts.inHeight, NodeSet.ConnectionType.Feedback);
            m_Set.Connect(m_AdvectionNodeList[neighbor[i].prevX], AdvectionNode.KernelPorts.outHeight, m_DiffusionNodeList[i], DiffusionNode.KernelPorts.inHeightPrevX, NodeSet.ConnectionType.Feedback);
            m_Set.Connect(m_AdvectionNodeList[neighbor[i].nextX], AdvectionNode.KernelPorts.outHeight, m_DiffusionNodeList[i], DiffusionNode.KernelPorts.inHeightNextX, NodeSet.ConnectionType.Feedback);
            m_Set.Connect(m_AdvectionNodeList[neighbor[i].prevZ], AdvectionNode.KernelPorts.outHeight, m_DiffusionNodeList[i], DiffusionNode.KernelPorts.inHeightPrevZ, NodeSet.ConnectionType.Feedback);
            m_Set.Connect(m_AdvectionNodeList[neighbor[i].nextZ], AdvectionNode.KernelPorts.outHeight, m_DiffusionNodeList[i], DiffusionNode.KernelPorts.inHeightNextZ, NodeSet.ConnectionType.Feedback);

            // Connect uniforms to 2nd stage (advection)
            m_Set.Connect(m_UniformNode, UniformNode.KernelPorts.outDeltaT, m_AdvectionNodeList[i], AdvectionNode.KernelPorts.inDeltaT);
            m_Set.Connect(m_UniformNode, UniformNode.KernelPorts.outVolumeToAdd, m_AdvectionNodeList[i], AdvectionNode.KernelPorts.inVolumeToAdd);

            // Connect diffusion output to 2nd stage advection inputs
            m_Set.Connect(m_DiffusionNodeList[i], DiffusionNode.KernelPorts.outSpeed, m_AdvectionNodeList[i], AdvectionNode.KernelPorts.inSpeed);            
        }

        SetViscosity(viscosity);
        SetDiffusionSpeed(diffusionSpeed);
    }

    public void Dispose()
    {
        for (int i = 0; i < vertices.Length; i++)
        {
            m_Set.Destroy(m_AdvectionNodeList[i]);
            m_Set.Destroy(m_DiffusionNodeList[i]);
            m_Set.ReleaseGraphValue(m_HeightGraphValues[i]);
        }    

        m_Set.Destroy(m_UniformNode);
    }

    public void SetViscosity(float viscosity)
    {
        m_Set.SetData(m_UniformNode, UniformNode.KernelPorts.inViscosity, viscosity);
    }

    public void SetDiffusionSpeed(float diffusionSpeed)
    {
        m_Set.SetData(m_UniformNode, UniformNode.KernelPorts.inDiffusionSpeed, diffusionSpeed);
    }

    public void ApplyForce(int indexToApply, float force, float mass)
    {
        m_Set.SendMessage(m_DiffusionNodeList[indexToApply], DiffusionNode.SimulationPorts.accelerationInput, force / mass);
    }

    // Phase 1
    public void ApplyForcesToSimulation(float deltaT)
    {
        /*
        foreach (Force force in forcesToApply)
        {
            // TODO: Reduce speed around
            speedY[force.index] += force.acceleration * deltaT;
        }
        */
    }       

    public void Diffusion(float deltaT)
    {
        // Nothing to do, done in DFG
    }

    // Phase 3
    public void Advection(float volumeToAddPerCell, float deltaT)
    {
        // Set uniforms
        m_Set.SetData(m_UniformNode, UniformNode.KernelPorts.inDeltaT, deltaT);
        m_Set.SetData(m_UniformNode, UniformNode.KernelPorts.inVolumeToAdd, volumeToAddPerCell);
        m_Set.Update();
    }

    public void ApplyToMesh()
    {
        for (int i = 0; i < vertices.Length; i++)
            vertices[i].y = m_Set.GetValueBlocking(m_HeightGraphValues[i]);
    }    
}

