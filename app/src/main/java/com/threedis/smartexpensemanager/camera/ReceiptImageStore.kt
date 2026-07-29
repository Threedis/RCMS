package com.threedis.smartexpensemanager.camera

import android.content.Context
import android.graphics.Bitmap
import android.graphics.BitmapFactory
import android.net.Uri
import dagger.hilt.android.qualifiers.ApplicationContext
import java.io.File
import java.io.FileOutputStream
import javax.inject.Inject
import javax.inject.Singleton

/** Persists receipt photos to app-private storage, compressing full images and generating thumbnails. */
@Singleton
class ReceiptImageStore @Inject constructor(
    @ApplicationContext private val context: Context
) {
    private val receiptsDir get() = File(context.getExternalFilesDir(null), "receipts").apply { mkdirs() }

    fun newCaptureFile(): File = File(receiptsDir, "receipt_${System.currentTimeMillis()}.jpg")

    /** Compresses [sourceUri] and stores full + thumbnail copies. Returns (fullPath, thumbnailPath). */
    fun storeCompressed(sourceUri: Uri, maxDimension: Int = 1600, thumbnailDimension: Int = 300): Pair<String, String> {
        val bitmap = decodeSampledBitmap(sourceUri, maxDimension)
        val fullFile = File(receiptsDir, "receipt_${System.currentTimeMillis()}_full.jpg")
        FileOutputStream(fullFile).use { out -> bitmap.compress(Bitmap.CompressFormat.JPEG, 80, out) }

        val thumb = scaleBitmap(bitmap, thumbnailDimension)
        val thumbFile = File(receiptsDir, "receipt_${System.currentTimeMillis()}_thumb.jpg")
        FileOutputStream(thumbFile).use { out -> thumb.compress(Bitmap.CompressFormat.JPEG, 70, out) }

        return fullFile.absolutePath to thumbFile.absolutePath
    }

    fun delete(path: String?) {
        if (path.isNullOrBlank()) return
        File(path).takeIf { it.exists() }?.delete()
    }

    private fun decodeSampledBitmap(uri: Uri, maxDimension: Int): Bitmap {
        val options = BitmapFactory.Options().apply { inJustDecodeBounds = true }
        context.contentResolver.openInputStream(uri)?.use { BitmapFactory.decodeStream(it, null, options) }

        var sampleSize = 1
        val (width, height) = options.outWidth to options.outHeight
        while ((width / sampleSize) > maxDimension || (height / sampleSize) > maxDimension) {
            sampleSize *= 2
        }

        val decodeOptions = BitmapFactory.Options().apply { inSampleSize = sampleSize }
        return context.contentResolver.openInputStream(uri)?.use {
            BitmapFactory.decodeStream(it, null, decodeOptions)
        } ?: error("Unable to decode image")
    }

    private fun scaleBitmap(bitmap: Bitmap, maxDimension: Int): Bitmap {
        val ratio = maxDimension.toFloat() / maxOf(bitmap.width, bitmap.height)
        if (ratio >= 1f) return bitmap
        val newWidth = (bitmap.width * ratio).toInt()
        val newHeight = (bitmap.height * ratio).toInt()
        return Bitmap.createScaledBitmap(bitmap, newWidth, newHeight, true)
    }
}
