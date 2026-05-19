package com.duelrecords.scan

import androidx.compose.runtime.Composable
import androidx.navigation.compose.NavHost
import androidx.navigation.compose.composable
import androidx.navigation.compose.rememberNavController
import com.duelrecords.scan.ui.screen.CollectionScreen
import com.duelrecords.scan.ui.screen.ScanScreen

@Composable
fun AppNavigation() {
    val navController = rememberNavController()

    NavHost(navController = navController, startDestination = "collection") {
        composable("collection") {
            CollectionScreen(onScanClick = { navController.navigate("scan") })
        }
        composable("scan") {
            ScanScreen(onBack = { navController.popBackStack() })
        }
    }
}
