package com.duelrecords.scan.data.model

data class Card(
    val id: Int,
    val nome: String,
    val tipo: String,
    val atributo: String?,
    val nivel: Int?,
    val ataque: Int?,
    val defesa: Int?,
    val tipoDeck: String,
    val raridade: String,
    val quantidade: Int,
    val colecao: String,
    val descricao: String?,
    val imagemUrl: String?,
    val dataCadastro: String
)

data class YgoCardSuggestion(
    val ygoId: Int,
    val name: String,
    val type: String?,
    val attribute: String?,
    val level: Int?,
    val attack: Int?,
    val defense: Int?,
    val imageUrlSmall: String?
)

data class YgoCardDetails(
    val ygoId: Int,
    val name: String,
    val type: String,
    val frameType: String?,
    val description: String?,
    val race: String?,
    val attribute: String?,
    val archetype: String?,
    val attack: Int?,
    val defense: Int?,
    val level: Int?,
    val humanReadableCardType: String?,
    val imageUrl: String?,
    val imageUrlSmall: String?,
    val imageUrlCropped: String?
)

data class ScanAddRequest(
    val ygoId: Int,
    val secao: String = "Main",
    val quantidade: Int = 1
)
