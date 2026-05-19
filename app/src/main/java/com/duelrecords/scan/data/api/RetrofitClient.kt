package com.duelrecords.scan.data.api

import okhttp3.OkHttpClient
import okhttp3.logging.HttpLoggingInterceptor
import retrofit2.Retrofit
import retrofit2.converter.gson.GsonConverterFactory

object RetrofitClient {

    // Emulador: 10.0.2.2 aponta para o localhost do seu computador
    // Celular físico na mesma rede: troque pelo IP do seu PC, ex: http://192.168.1.100:5001/
    private const val BASE_URL = "http://10.0.2.2:5001/"

    private val httpClient = OkHttpClient.Builder()
        .addInterceptor(HttpLoggingInterceptor().apply {
            level = HttpLoggingInterceptor.Level.BODY
        })
        .build()

    val api: DuelRecordsApi by lazy {
        Retrofit.Builder()
            .baseUrl(BASE_URL)
            .client(httpClient)
            .addConverterFactory(GsonConverterFactory.create())
            .build()
            .create(DuelRecordsApi::class.java)
    }
}
