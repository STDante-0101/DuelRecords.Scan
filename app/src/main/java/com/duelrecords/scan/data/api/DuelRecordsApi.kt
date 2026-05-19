package com.duelrecords.scan.data.api

import com.duelrecords.scan.data.model.Card
import com.duelrecords.scan.data.model.ScanAddRequest
import com.duelrecords.scan.data.model.YgoCardDetails
import com.duelrecords.scan.data.model.YgoCardSuggestion
import retrofit2.http.Body
import retrofit2.http.GET
import retrofit2.http.POST
import retrofit2.http.Path
import retrofit2.http.Query

interface DuelRecordsApi {

    @GET("api/cards")
    suspend fun getCards(): List<Card>

    @GET("api/ygo/cards/buscar")
    suspend fun searchCards(@Query("nome") nome: String): List<YgoCardSuggestion>

    @GET("api/ygo/cards/{ygoId}")
    suspend fun getCardDetails(@Path("ygoId") ygoId: Int): YgoCardDetails

    @POST("api/scan/cards/from-ygo/{ygoId}")
    suspend fun addToCollection(@Path("ygoId") ygoId: Int): Card

    @POST("api/scan/decks/{deckId}/add")
    suspend fun addToDeck(@Path("deckId") deckId: Int, @Body dto: ScanAddRequest): Any
}
