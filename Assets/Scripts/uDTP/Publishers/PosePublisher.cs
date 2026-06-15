using RSMA.uDTP;
using RSMA.uDTP.Topics;
using System;
using UnityEngine;

public class TransformPublisher : MonoBehaviour
{
    private RSMA.uDTP.Topics.Pose pose;
    public string topicName = "Pose";

    void Start()
    {
        pose = new RSMA.uDTP.Topics.Pose();
        pose.position = transform.position;
        pose.rotation = transform.rotation;
        pose.timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        DataBroker.Publish(topicName, pose);
    }

    void Update()
    {
        pose.position = transform.position;
        pose.rotation = transform.rotation;
        pose.timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        DataBroker.Publish(topicName, pose);
    }
}
