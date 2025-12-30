using UnityEngine;
using XCharts.Runtime;

public class SuspensionDraw : MonoBehaviour
{

    LineChart chart;
    // Start is called before the first frame update
    void Start()
    {
        chart = GetComponent<LineChart>();
        chart.RemoveData();
        chart.AddSerie<Line>("Suspension");

        var xAxis = chart.EnsureChartComponent<XAxis>();
        xAxis.boundaryGap = true;
        xAxis.type = Axis.AxisType.Time;

        var yAxis = chart.EnsureChartComponent<YAxis>();
        yAxis.type = Axis.AxisType.Value;
    }

    // Update is called once per frame
    public void UpdateChart(float yPos, float time)
    {
        chart.AddXAxisData(time.ToString());
        chart.AddData(0, yPos);
    }
}
