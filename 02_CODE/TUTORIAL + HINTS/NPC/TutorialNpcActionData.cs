using System;
using UnityEngine;

[Serializable]
public class TutorialNpcActionData
{
    public TutorialNpcActionType actionType = TutorialNpcActionType.None;

    [Header("Targets")]
    public Transform targetTransform;
    public string targetId; 
    public Vector3 worldPoint;

    [Header("Movement")]
    public float moveStoppingDistance = 0.25f;
    public float moveTimeoutSeconds = 10f;
    public bool snapYToNpc = true;

    [Header("Follow")]
    public float followStopDistance = 1.5f;
    public float followMaxDuration = 10f;

    [Header("Facing")]
    public float turnSpeedDegPerSec = 540f;

    [Header("Animation")]
    public string animatorParamName;
    public bool animatorBoolValue = true;

    [Header("Move Style")]
    public TutorialNpcMoveStyle moveStyle = TutorialNpcMoveStyle.Walk;
    public float walkSpeed = 1.6f;
    public float jogSpeed = 2.6f;
    public float runSpeed = 3.8f;
    public float acceleration = 12f;
    public float angularSpeed = 720f;

    [Header("Follow Advanced")]
    public bool followContinuously = false;          // keep running until CancelCurrentAction / next step
    public bool sitWhenNearPlayer = true;            // sit when inside stop distance
    public float standUpDistance = 2.2f;             // if player goes farther than this, stand and resume move
    public float followRepathInterval = 0.15f; 
}