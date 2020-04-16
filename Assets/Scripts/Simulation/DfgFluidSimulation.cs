using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.DataFlowGraph;
using Unity.Burst;
using Unity.Jobs;
using Unity.Collections;

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

public class AdvectionNode : NodeDefinition<AdvectionNode.InstanceData, AdvectionNode.SimPorts, AdvectionNode.KernelData, AdvectionNode.Ports, AdvectionNode.GraphKernel>, IMsgHandler<float>
{
    public struct KernelData : IKernelData
    {
        public float initHeight;
    }

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

    public struct SimPorts : ISimulationPortDefinition
    {
        public MessageInput<AdvectionNode, float> initialHeight;
    }

    public void HandleMessage(in MessageContext ctx, in float msg)
    {
        ref KernelData kernelData = ref GetKernelData(ctx.Handle);
        kernelData.initHeight = msg;
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
            height = data.initHeight + Mathf.Max(height + volumeToAdd + speed * deltaT, 0.0f);

            // Write outputs
            ref var outputHeight = ref ctx.Resolve(ref ports.outHeight);
            outputHeight = height;
        }
    }
}

public class GatherNode : NodeDefinition<GatherNode.InstanceData, GatherNode.KernelData, GatherNode.Ports, GatherNode.GraphKernel>
{
    public struct KernelData : IKernelData { }

    public struct InstanceData : INodeData { }

    public struct Ports : IKernelPortDefinition
    {
        public PortArray<DataInput<GatherNode, float>>  inHeight;
        public DataOutput<GatherNode, Buffer<float>>    outHeightBuffer;
        public DataOutput<GatherNode, float>            outTotalVolume;
    }

    public struct GraphKernel : IGraphKernel<KernelData, Ports>
    {
        [BurstCompile]
        public void Execute(RenderContext ctx, KernelData data, ref Ports ports)
        {
            var array = ctx.Resolve(ref ports.outHeightBuffer);
            var portArray = ctx.Resolve(ports.inHeight);

            float totalVolume = 0.0f;
            for (int i = 0; i < array.Length; ++i)
            {
                array[i] = portArray[i];
                totalVolume += portArray[i];
            }

            // Write outputs
            ref var outputTotalVolume = ref ctx.Resolve(ref ports.outTotalVolume);
            outputTotalVolume = totalVolume;
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
        public DataInput<UniformNode, float> inTotalVolume;
        public DataInput<UniformNode, float> inExpectedVolume;
        public DataInput<UniformNode, uint>  inSimCellCount;

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
            //volumeToAdd = ctx.Resolve(ports.inVolumeToAdd);
            float expectedVolume = ctx.Resolve(ports.inExpectedVolume);
            float totalVolume = ctx.Resolve(ports.inTotalVolume);
            uint simSize = ctx.Resolve(ports.inSimCellCount);
            volumeToAdd = (expectedVolume - totalVolume) / (float)simSize;
        }
    }
}

[System.Serializable]
public class DfgSimulation : FluidSimulationInterface
{
    private NativeArray<Vector3> m_Vertices;
    private Neighbor[] m_Neighbor = null;
    NodeHandle<UniformNode> m_UniformNode;
    NodeHandle<GatherNode> m_GatherNode;
    List<NodeHandle<DiffusionNode>> m_DiffusionNodeList = new List<NodeHandle<DiffusionNode>>();
    List<NodeHandle<AdvectionNode>> m_AdvectionNodeList = new List<NodeHandle<AdvectionNode>>();
    //List<GraphValue<float>> m_HeightGraphValues = new List<GraphValue<float>>();
    GraphValue<Buffer<float>> gatherNodeGraphValue;
    NodeSet m_Set;

    public DfgSimulation(Neighbor[] neighbor, ref NativeArray<Vector3> vertices)
    {
        m_Vertices = vertices;
        m_Neighbor = neighbor;

        m_Set = new NodeSet();
        //m_Set.RendererModel = NodeSet.RenderExecutionModel.Islands;
        m_Set.RendererModel = NodeSet.RenderExecutionModel.SingleThreaded;

        // Create uniform node (the one with all shared values)
        m_UniformNode = m_Set.Create<UniformNode>();

        // Create gather node (the one that will gather an array at the end)
        m_GatherNode = m_Set.Create<GatherNode>();
        m_Set.SetPortArraySize(m_GatherNode, GatherNode.KernelPorts.inHeight, (ushort)m_Vertices.Length);
        m_Set.SetBufferSize(m_GatherNode, GatherNode.KernelPorts.outHeightBuffer, Buffer<float>.SizeRequest(m_Vertices.Length));
        gatherNodeGraphValue = m_Set.CreateGraphValue(m_GatherNode, GatherNode.KernelPorts.outHeightBuffer);
        // Connect gather node output to uniform volume input
        m_Set.Connect(m_GatherNode, GatherNode.KernelPorts.outTotalVolume, m_UniformNode, UniformNode.KernelPorts.inTotalVolume, NodeSet.ConnectionType.Feedback);

        for (uint i = 0; i < vertices.Length; i++)
        {
            // Create Diffusions nodes
            m_DiffusionNodeList.Add(m_Set.Create<DiffusionNode>());
            // Create Advections nodes
            m_AdvectionNodeList.Add(m_Set.Create<AdvectionNode>());
            // Create Height reading graph values
            //m_HeightGraphValues.Add(m_Set.CreateGraphValue(m_AdvectionNodeList[(int)i], AdvectionNode.KernelPorts.outHeight));
        }

        // Connects Nodes; 1 layer = diffusion, 2nd layer = advection
        for (int i = 0; i < vertices.Length; i++)
        {
            // Connect uniforms to 1st stage (diffusion)
            m_Set.Connect(m_UniformNode, UniformNode.KernelPorts.outDeltaT, m_DiffusionNodeList[i], DiffusionNode.KernelPorts.inDeltaT);
            m_Set.Connect(m_UniformNode, UniformNode.KernelPorts.outDiffusionSpeed, m_DiffusionNodeList[i], DiffusionNode.KernelPorts.inDiffusionSpeed);
            m_Set.Connect(m_UniformNode, UniformNode.KernelPorts.outViscosity, m_DiffusionNodeList[i], DiffusionNode.KernelPorts.inViscosity);

            // Connect advection output to gather node
            m_Set.Connect(m_AdvectionNodeList[i], AdvectionNode.KernelPorts.outHeight, m_GatherNode, GatherNode.KernelPorts.inHeight, i);

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

            m_Set.SendMessage(m_AdvectionNodeList[i], AdvectionNode.SimulationPorts.initialHeight, m_Vertices[i].y);
        }

        m_Set.SetData(m_UniformNode, UniformNode.KernelPorts.inSimCellCount, (uint)vertices.Length);

        // First dry run
        m_Set.Update(); 
        for (int i = 0; i < vertices.Length; i++)
            m_Set.SendMessage(m_AdvectionNodeList[i], AdvectionNode.SimulationPorts.initialHeight, 0.0f);
    }

    public void Dispose()
    {
        for (int i = 0; i < m_Vertices.Length; i++)
        {
            m_Set.Destroy(m_AdvectionNodeList[i]);
            m_Set.Destroy(m_DiffusionNodeList[i]);
            //m_Set.ReleaseGraphValue(m_HeightGraphValues[i]);
        }

        m_Set.ReleaseGraphValue(gatherNodeGraphValue);
        m_Set.Destroy(m_GatherNode);
        m_Set.Destroy(m_UniformNode);
        m_Set.Dispose();
    }

    public void ApplyForce(int indexToApply, float force, float mass)
    {
        float acceleration = force / mass;
        m_Set.SendMessage(m_DiffusionNodeList[indexToApply], DiffusionNode.SimulationPorts.accelerationInput, acceleration);
        /*
        acceleration = acceleration / -4.0f;
        m_Set.SendMessage(m_DiffusionNodeList[m_Neighbor[indexToApply].prevX], DiffusionNode.SimulationPorts.accelerationInput, acceleration);
        m_Set.SendMessage(m_DiffusionNodeList[m_Neighbor[indexToApply].nextX], DiffusionNode.SimulationPorts.accelerationInput, acceleration);
        m_Set.SendMessage(m_DiffusionNodeList[m_Neighbor[indexToApply].prevZ], DiffusionNode.SimulationPorts.accelerationInput, acceleration);
        m_Set.SendMessage(m_DiffusionNodeList[m_Neighbor[indexToApply].nextZ], DiffusionNode.SimulationPorts.accelerationInput, acceleration);
        */
    }

    // Phase 1
    public void ApplyForcesToSimulation(float deltaT)
    {
        // Embedded in diffusion phase
        // So, nothing to do here
    }
               
    [BurstCompile(CompileSynchronously = true)]
    struct GraphBufferReadbackJob : IJob
    {
        public GraphValueResolver           Resolver;
        public NativeArray<Vector3>         Result;
        public GraphValue<Buffer<float>>    Value;

        public void Execute()
        {
            for(int i = 0; i < Result.Length; i++)
                Result[i] = new Vector3(Result[i].x, Resolver.Resolve(Value)[i], Result[i].z);
        }
    }

    public void WaitForVerticeUpdate()
    {        
        GraphBufferReadbackJob job;

        job.Value       = gatherNodeGraphValue;
        job.Result      = m_Vertices;
        job.Resolver    = m_Set.GetGraphValueResolver(out var valueResolverDependency);

        JobHandle dfgJob = job.Schedule(valueResolverDependency);
        m_Set.InjectDependencyFromConsumer(dfgJob);

        dfgJob.Complete();
    }
    

    public void Simulate(float targetTotalheight, float diffusionSpeed, float viscosity, float deltaT)
    {
        // Set uniforms
        m_Set.SetData(m_UniformNode, UniformNode.KernelPorts.inDeltaT, deltaT);
        m_Set.SetData(m_UniformNode, UniformNode.KernelPorts.inDiffusionSpeed, diffusionSpeed);
        m_Set.SetData(m_UniformNode, UniformNode.KernelPorts.inViscosity, viscosity);
        m_Set.SetData(m_UniformNode, UniformNode.KernelPorts.inExpectedVolume, targetTotalheight);              

        UnityEngine.Profiling.Profiler.BeginSample("ApplyForcesToSimulation");
        ApplyForcesToSimulation(deltaT);
        UnityEngine.Profiling.Profiler.EndSample();

        UnityEngine.Profiling.Profiler.BeginSample("Simulate");
        m_Set.Update();
        UnityEngine.Profiling.Profiler.EndSample();
    }
}

