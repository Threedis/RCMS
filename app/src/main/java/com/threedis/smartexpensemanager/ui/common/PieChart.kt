package com.threedis.smartexpensemanager.ui.common

import androidx.compose.foundation.Canvas
import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.unit.Dp
import androidx.compose.ui.unit.dp

data class PieSlice(val label: String, val value: Double, val color: Color)

private val paletteColors = listOf(
    Color(0xFF6750A4), Color(0xFF7D5260), Color(0xFF386A20), Color(0xFFB3261E),
    Color(0xFF00696C), Color(0xFF984061), Color(0xFF785900), Color(0xFF31628C),
    Color(0xFF8C4A00), Color(0xFF4A6D24), Color(0xFF9C4146), Color(0xFF006874)
)

fun colorForIndex(index: Int): Color = paletteColors[index % paletteColors.size]

/** Renders a simple, dependency-free pie chart using only Compose's Canvas. */
@Composable
fun PieChart(
    slices: List<PieSlice>,
    modifier: Modifier = Modifier,
    diameter: Dp = 180.dp
) {
    val total = slices.sumOf { it.value }
    Box(modifier = modifier.size(diameter)) {
        Canvas(modifier = Modifier.size(diameter)) {
            if (total <= 0.0) return@Canvas
            var startAngle = -90f
            for (slice in slices) {
                val sweep = (slice.value / total * 360.0).toFloat()
                drawArc(
                    color = slice.color,
                    startAngle = startAngle,
                    sweepAngle = sweep,
                    useCenter = true
                )
                startAngle += sweep
            }
        }
    }
}

@Composable
fun PieChartLegendRow(slice: PieSlice, percent: Double) {
    Row(
        modifier = Modifier.fillMaxWidth(),
        horizontalArrangement = Arrangement.SpaceBetween,
        verticalAlignment = Alignment.CenterVertically
    ) {
        Row(verticalAlignment = Alignment.CenterVertically) {
            Box(
                modifier = Modifier
                    .size(12.dp)
                    .background(slice.color, CircleShape)
            )
            Spacer(Modifier.size(8.dp))
            Text(slice.label)
        }
        Text("${"%.1f".format(percent)}%  (${"%.2f".format(slice.value)})")
    }
}
