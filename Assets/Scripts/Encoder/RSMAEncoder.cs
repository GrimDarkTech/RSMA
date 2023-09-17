using UnityEditor;
using UnityEngine;

public class RSMAEncoder : RSMADataTransferSlave
{
    //����� ������
    public EncoderType EncoderTypeObj { get; set; }
    public EncoderRangeType EncoderRangeType { get; set; }
    public AxisEnum AxisDirection { get; set; } //� ����� ������� ����� ��������� ������� ������������ ������
    public EncoderOutput OutPut { get; set; } = EncoderOutput.Output3;
    public bool AutoAxis { get; set; } //�������������� ����������� ������� �������


    //������ ���������������� ��������
    public float Distance = 2;
    public float EncoderResolution = 1;

    public GameObject Shaft;
    public HingeJoint Motor;

    void FixedUpdate()
    {
        if (Motor == null) return;

        data = "";
        if ((int)OutPut >= 0) data += $"{GetVelocity()};";
        if ((int)OutPut >= 1) data += $"{GetSide()};";
        if ((int)OutPut >= 2) data += $"{GetAngle()};";

        float steps = 1 / Time.fixedDeltaTime;
        float speed = Motor.motor.targetVelocity;
        float side = Distance < 0 ? 1 : -1;


        Shaft.transform.Rotate(0, 0, side * (speed / steps), Space.Self);
    }

    /// <summary>
    /// �������� �������� �������� ���� � ��������/�������
    /// </summary>
    public float GetVelocity()
    {
        return Motor.velocity;
    }

    /// <summary>
    /// �������� ����������� ����
    /// </summary>
    public int GetSide()
    {
        if (Motor.velocity == 0) return 0;
        if (Motor.motor.targetVelocity < 0) return 1;
        if (Motor.motor.targetVelocity > 0) return 2;
        return 0;
    }

    public string GetHZ()
    {
        if (Motor == null || !Motor.useMotor) return "Motor is NULL";
        return $"{(int)(Motor.velocity * EncoderResolution)} ��";
    }

    public string GetMeasureAngle()
    {
        if (EncoderResolution == 0) return "NaN";
        return $"{360 / EncoderResolution}�";
    }

    /// <summary>
    /// �������� ���� ��������
    /// </summary>
    /// <returns></returns>
    public float GetAngle()
    {
        float current_angle = Motor.angle;
        float measure_angle = 360 / EncoderResolution;
        if (Motor.angle < 0)
        {
            current_angle = 360 + Motor.angle;
        }
        if (EncoderTypeObj == EncoderType.Absolute) return current_angle;
        if (EncoderTypeObj == EncoderType.Incremental) return ((int)(current_angle / measure_angle)) * measure_angle;
        return 0;
    }

    public override string SendData()
    {
        return (data);
    }
}

[CustomEditor(typeof(RSMAEncoder))]
public class RSMAEncoderEditor : Editor
{

    public override void OnInspectorGUI()
    {
        RSMAEncoder rsma_encoder = (RSMAEncoder)target;
        rsma_encoder.EncoderTypeObj = (EncoderType)EditorGUILayout.EnumPopup("��� ��������: ", rsma_encoder.EncoderTypeObj);

        SharedFunctions(rsma_encoder);

        if (rsma_encoder.EncoderTypeObj == EncoderType.Incremental) IncrementalEncoder(rsma_encoder);
        if (rsma_encoder.EncoderTypeObj == EncoderType.Absolute) AbsouluteEncoder(rsma_encoder);

    }

    private void IncrementalEncoder(RSMAEncoder rsma_encoder)
    {
        EditorGUILayout.LabelField("��������������� �������");

        float Local_EncoderRes = EditorGUILayout.FloatField("���������� �������� ���/��: ", rsma_encoder.EncoderResolution); //���-�� ��������� �� ������
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField($"������� �������� ���������: {rsma_encoder.GetHZ()}"); //������ � ������� ��������
        EditorGUILayout.LabelField($"���� �� �������: {rsma_encoder.GetMeasureAngle()}"); //������ �� ���� �� �������
        EditorGUILayout.Space(5);
        if ((int)rsma_encoder.OutPut >= 0) EditorGUILayout.LabelField($"����� 1. �������� �������� ����: {rsma_encoder.GetVelocity()} ����/���");
        if ((int)rsma_encoder.OutPut >= 1) EditorGUILayout.LabelField($"����� 2. ����������� ��������: {(rsma_encoder.GetSide() == 1 ? "�����" : (rsma_encoder.GetSide() == 2 ? "����" : "�� ���������"))}");
        if ((int)rsma_encoder.OutPut >= 2) EditorGUILayout.LabelField($"����� 3. ���� ��������: {rsma_encoder.GetAngle()}�");

        EditorGUILayout.HelpBox("������������ ������� - ��������� ��������, ���������� \n������� ������������� �������� ���� �� ������������ ����.", MessageType.Info);

        if (Local_EncoderRes < 0) Local_EncoderRes = 0;
        rsma_encoder.EncoderResolution = Local_EncoderRes;
    }

    private void AbsouluteEncoder(RSMAEncoder rsma_encoder)
    {
        EditorGUILayout.LabelField("���������� ������� �������");

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField($"������� �������� ���������: {rsma_encoder.GetHZ()}"); //������ � ������� ��������
        EditorGUILayout.Space(5);
        if ((int)rsma_encoder.OutPut >= 0) EditorGUILayout.LabelField($"����� 1. �������� �������� ����: {rsma_encoder.GetVelocity()} ����/���");
        if ((int)rsma_encoder.OutPut >= 1) EditorGUILayout.LabelField($"����� 2. ����������� ��������: {(rsma_encoder.GetSide() == 1 ? "�����" : (rsma_encoder.GetSide() == 2 ? "����" : "�� ���������"))}");
        if ((int)rsma_encoder.OutPut >= 2) EditorGUILayout.LabelField($"����� 3. ���� ��������: {rsma_encoder.GetAngle()}�");

        EditorGUILayout.HelpBox("���������� ������� � ��� ������ �������� ���������, \n������� ����� ���������� � ��������� � ���� ��������������� ��������� ����. \n������ ��� �������� ���������� � �������� ��������� ���������� ������� ���������.", MessageType.Info);
    }

    private void SharedFunctions(RSMAEncoder rsma_encoder)
    {
        rsma_encoder.Motor = (HingeJoint)EditorGUILayout.ObjectField("�����:", rsma_encoder.Motor, typeof(HingeJoint));
        rsma_encoder.Shaft = (GameObject)EditorGUILayout.ObjectField("���:", rsma_encoder.Shaft, typeof(GameObject));
        EditorGUILayout.Space(10);
        rsma_encoder.AutoAxis = EditorGUILayout.Toggle("���� ����������� ������������ ���: ", rsma_encoder.AutoAxis);
        if (!rsma_encoder.AutoAxis)
            rsma_encoder.AxisDirection = (AxisEnum)EditorGUILayout.EnumPopup("������������ ���: ", rsma_encoder.AxisDirection);
        rsma_encoder.Distance = EditorGUILayout.FloatField("���������� �� ������: ", rsma_encoder.Distance);
        rsma_encoder.OutPut = (EncoderOutput)EditorGUILayout.EnumPopup("���������� �������: ", rsma_encoder.OutPut);

        SetAxisPos(rsma_encoder);

        EditorGUILayout.Space(20);
    }

    private void SetAxisPos(RSMAEncoder rsma_encoder)
    {
        if (rsma_encoder.Motor == null) return;
        if (Application.isPlaying) return;

        rsma_encoder.transform.position = rsma_encoder.Motor.transform.position;

        if (rsma_encoder.AutoAxis)
        {
            var local_axis =
                rsma_encoder.Motor.axis.normalized.x * rsma_encoder.Motor.transform.right +
                rsma_encoder.Motor.axis.normalized.y * rsma_encoder.Motor.transform.up +
                rsma_encoder.Motor.axis.normalized.z * rsma_encoder.Motor.transform.forward
                ;

            rsma_encoder.transform.position += local_axis * rsma_encoder.Distance;
            rsma_encoder.transform.LookAt(rsma_encoder.Motor.transform.position);
            return;
        }

        switch (rsma_encoder.AxisDirection)
        {
            case AxisEnum.X:
                rsma_encoder.transform.position += rsma_encoder.Motor.transform.right * rsma_encoder.Distance;
                break;
            case AxisEnum.Y:
                rsma_encoder.transform.position += rsma_encoder.Motor.transform.up * rsma_encoder.Distance;
                break;
            case AxisEnum.Z:
                rsma_encoder.transform.position += rsma_encoder.Motor.transform.forward * rsma_encoder.Distance;
                break;
        }
        rsma_encoder.transform.LookAt(rsma_encoder.Motor.transform.position);
    }

    //private void SetAxis
}

/// <summary>
/// 1-������ ������� - ���������� ������� �������� ���������
/// 2-������ ������� - ���������� 1���� � ����������� �������� ���������
/// 3-������ ������� - ���������� 1���� � 2���� � ���� ��������
/// </summary>
public enum EncoderOutput
{
    Output1 = 0,
    Output2 = 1,
    Output3 = 2
}

public enum EncoderType
{
    Incremental = 0,
    Absolute = 1
}

public enum EncoderRangeType
{
    Limit = 0,
    Unlimit = 1
}