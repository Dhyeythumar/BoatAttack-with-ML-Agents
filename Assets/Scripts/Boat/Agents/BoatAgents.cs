using System;
using UnityEngine;
using BoatAttack;
using System.Text.RegularExpressions;
using Cinemachine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Policies;

// This script is for Self-Play strategy
public class BoatAgents : Agent
{
    public enum Team
    {
        Red = 0,
        Blue = 1
    }
    [HideInInspector] public Team team;

	public Engine engine;
    public Transform initialPos; // initial pos of the boat
    public TeamManager teamManager;
    public CinemachineVirtualCamera agentVirtualCam;
    private int m_AgentIndex;

    [NonSerialized] public float speed = 0.0f;
    private Vector3 lastPosition;

    BehaviorParameters m_BehaviorParameters;

    /// <summary>
    /// Initialize the agent's parameters when it is invoked.
    /// </summary>
    public override void Initialize() {

        m_BehaviorParameters = gameObject.GetComponent<BehaviorParameters>();
        if (m_BehaviorParameters.TeamId == (int)Team.Red) {
            team = Team.Red;
        }
        else {
            team = Team.Blue;
        }

        agentVirtualCam.enabled = false;
        // Register the agent to the Team Manager's activeAgents list.
        var activeAgent = new ActiveAgents {
            agentScript = this,
            agentCam = agentVirtualCam,
            checkpoint = 0
        };
        teamManager.activeAgents.Add(activeAgent);
        m_AgentIndex = teamManager.activeAgents.IndexOf(activeAgent);

    	TryGetComponent(out engine.RB);
        lastPosition = this.transform.position;
    }

    private void FixedUpdate() {
    	speed = Mathf.RoundToInt(Vector3.Distance(this.transform.position, lastPosition) / Time.fixedDeltaTime);
    	lastPosition = this.transform.position;
    }

    /// <summary>
    /// Add agent's vectorized observation using this overridden method. 
    /// </summary>
    public override void CollectObservations(VectorSensor sensor)
    {
        // var localVelocity = transform.InverseTransformDirection(engine.RB.velocity);
        // sensor.AddObservation(localVelocity);
        sensor.AddObservation(speed);
    }

    /// <summary>
    /// Apply actions generated by the model. [Called every step]
    /// </summary>
    public override void OnActionReceived(float[] vectorAction)
    {
        engine.Accelerate(vectorAction[0]); // boat's throttle/acceleration
        engine.Turn(vectorAction[1]); // boat's steering

        // Give the Reward if boat agent is moving
        AddReward(speed * .001f);  // TODO :: might add -ve reward if agent is not moving !!
    }

    /// <summary>
    /// Heuristic method used to manually test the agent's action.
    /// Called after every 5 step when NN model is not attached.
    /// </summary>
    public override void Heuristic(float[] actionsOut)
    {
        actionsOut[0] = Input.GetAxis("Vertical"); // boat's throttle
        actionsOut[1] = Input.GetAxis("Horizontal"); // boat's steering
    }

    /// <summary>
    /// Setup the env on the start of each episode.  
    /// </summary>
    public override void OnEpisodeBegin()
    {
        _resetBoatPosition();
    }

    private void _resetBoatPosition()
    {
        engine.RB.velocity = Vector3.zero;
        engine.RB.angularVelocity = Vector3.zero;
        engine.RB.position = initialPos.position;
        engine.RB.rotation = initialPos.rotation;
    }

    private void OnCollisionEnter(Collision collision)
    {
        #if UNITY_EDITOR
            if(!(collision.gameObject.CompareTag("Boundary") || collision.gameObject.CompareTag("Ground") || collision.gameObject.CompareTag("Boat"))) {
                Debug.Log(collision.gameObject.name + " " + collision.gameObject.tag);
            }
        #endif
        AddReward(-1f);
    }

    public void addCheckpointReward(string checkpointName) {
    	// Debug.Log("Reached { " + checkpointName + " } !!");
    	if(checkpointName == "endPosition") {
            teamManager.startNewEpisode(team); // pass the winning team;
    	}
    	else {
    		Match match = Regex.Match(checkpointName, @"\d+");
			if (match.Success) {
                teamManager.activeAgents[m_AgentIndex].checkpoint = int.Parse(match.Value);
    			AddReward(int.Parse(match.Value) * (1 / 100000));
            }
    	}
    }
}
