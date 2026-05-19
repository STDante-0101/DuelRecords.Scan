package com.duelrecords.scan.viewmodel

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.duelrecords.scan.data.api.RetrofitClient
import com.duelrecords.scan.data.model.Card
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.launch

class CollectionViewModel : ViewModel() {

    private val _cards = MutableStateFlow<List<Card>>(emptyList())
    val cards: StateFlow<List<Card>> = _cards

    private val _isLoading = MutableStateFlow(false)
    val isLoading: StateFlow<Boolean> = _isLoading

    private val _error = MutableStateFlow<String?>(null)
    val error: StateFlow<String?> = _error

    init {
        loadCards()
    }

    fun loadCards() {
        viewModelScope.launch {
            _isLoading.value = true
            _error.value = null
            try {
                _cards.value = RetrofitClient.api.getCards()
            } catch (e: Exception) {
                _error.value = "Erro ao carregar coleção: ${e.message}"
            } finally {
                _isLoading.value = false
            }
        }
    }
}
